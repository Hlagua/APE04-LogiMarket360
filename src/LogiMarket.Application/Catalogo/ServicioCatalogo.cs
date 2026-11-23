using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;

namespace LogiMarket.Application.Catalogo;

public sealed record ProductoEdicion(string Sku, string Nombre, string Categoria, string Descripcion, decimal Precio,
    decimal PesoKg, int Stock);

public sealed record ComponenteKitDto(string Codigo, string Nombre, bool EsKit, int Cantidad);

public sealed record KitDto(int Id, string Codigo, string Nombre, decimal Precio, decimal PesoKg, int StockDisponible,
    IReadOnlyList<ComponenteKitDto> Componentes, string Composicion);

public sealed record ElementoVendibleDto(string Codigo, string Nombre, bool EsKit, decimal Precio, int StockDisponible)
{
    public override string ToString() => $"{Codigo} - {Nombre} (USD {Precio:0.00})";
}

public sealed record DesgloseDto(string Codigo, decimal Precio, decimal PesoKg, IReadOnlyList<ComponenteFisico> ComponentesIterador,
    IReadOnlyList<LineaDesglose> LineasVisitor, IReadOnlyList<string> KitsVisitados, string ResumenPeso);

/// <summary>Comandos del catálogo: productos, stock y kits (Composite con detección de ciclos). Las consultas están en ServicioConsultas.</summary>
public sealed class ServicioCatalogo : IServicioCatalogo
{
    private readonly IProductoRepositorio _productos;
    private readonly IKitRepositorio _kits;
    private readonly IMovimientoInventarioRepositorio _movimientos;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly ISesionActual _sesion;
    private readonly IReloj _reloj;

    public ServicioCatalogo(IProductoRepositorio productos, IKitRepositorio kits, IMovimientoInventarioRepositorio movimientos,
        IAuditoriaRepositorio auditoria, IUnidadDeTrabajo uow,
        IServicioAutorizacion autorizacion, ISesionActual sesion, IReloj reloj)
    {
        _productos = productos;
        _kits = kits;
        _movimientos = movimientos;
        _auditoria = auditoria;
        _uow = uow;
        _autorizacion = autorizacion;
        _sesion = sesion;
        _reloj = reloj;
    }

    // ---------------- Productos ----------------

    public async Task<ResultadoOperacion> CrearProductoAsync(ProductoEdicion datos, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var producto = Producto.Crear(datos.Sku, datos.Nombre, datos.Categoria, datos.Descripcion, datos.Precio, datos.PesoKg, datos.Stock);
        if (await _productos.ExisteSkuAsync(producto.Sku, ct) || await _kits.ExisteCodigoAsync(producto.Sku, ct))
            return ResultadoOperacion.Fallo("SKU_DUPLICADO", $"El SKU {producto.Sku} ya existe.");

        await _uow.EnTransaccionAsync(async c =>
        {
            _productos.Agregar(producto);
            await _uow.GuardarCambiosAsync(c);
            if (producto.Stock > 0)
                _movimientos.Agregar(new MovimientoInventario(producto.Id, producto.Sku, producto.Stock, producto.Stock,
                    MotivoMovimiento.INICIAL, "Alta de producto", _reloj.Ahora, _sesion.NombreUsuario));
            Auditar("PRODUCTO_CREADO", $"{producto.Sku} {producto.Nombre} USD {producto.Precio:0.00}");
            await _uow.GuardarCambiosAsync(c);
            return true;
        }, ct);
        return ResultadoOperacion.Ok($"Producto {producto.Sku} creado.");
    }

    public async Task<ResultadoOperacion> ActualizarProductoAsync(ProductoEdicion datos, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var producto = await _productos.ObtenerPorSkuAsync(datos.Sku, ct)
                       ?? throw new ExcepcionAplicacion("PRODUCTO_NO_EXISTE", $"No existe el SKU {datos.Sku}.");
        producto.Actualizar(datos.Nombre, datos.Categoria, datos.Descripcion, datos.Precio, datos.PesoKg);
        Auditar("PRODUCTO_ACTUALIZADO", $"{producto.Sku} precio actual USD {producto.Precio:0.00} (las ventas conservan su instantánea)");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Producto {producto.Sku} actualizado.");
    }

    public async Task<ResultadoOperacion> CambiarEstadoProductoAsync(string sku, bool activo, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var producto = await _productos.ObtenerPorSkuAsync(sku, ct)
                       ?? throw new ExcepcionAplicacion("PRODUCTO_NO_EXISTE", $"No existe el SKU {sku}.");
        if (activo) producto.Activar(); else producto.Desactivar();
        Auditar(activo ? "PRODUCTO_ACTIVADO" : "PRODUCTO_DESACTIVADO", producto.Sku);
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Producto {producto.Sku} {(activo ? "activado" : "desactivado")}.");
    }

    public async Task<ResultadoOperacion> AjustarStockAsync(string sku, int nuevoStock, string motivo, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.AjustarInventario);
        var producto = await _productos.ObtenerPorSkuAsync(sku, ct)
                       ?? throw new ExcepcionAplicacion("PRODUCTO_NO_EXISTE", $"No existe el SKU {sku}.");
        var diferencia = producto.AjustarStock(nuevoStock);
        if (diferencia == 0) return ResultadoOperacion.Ok("Sin cambios de stock.");
        _movimientos.Agregar(new MovimientoInventario(producto.Id, producto.Sku, diferencia, producto.Stock,
            MotivoMovimiento.AJUSTE, string.IsNullOrWhiteSpace(motivo) ? "Ajuste manual" : motivo.Trim(), _reloj.Ahora, _sesion.NombreUsuario));
        Auditar("STOCK_AJUSTADO", $"{producto.Sku} {diferencia:+#;-#} → {producto.Stock}");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Stock de {producto.Sku} ajustado a {producto.Stock}.");
    }

    // ---------------- Kits (Composite) ----------------

    public async Task<ResultadoOperacion> CrearKitAsync(string codigo, string nombre,
        IReadOnlyList<(string Codigo, int Cantidad)> componentes, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var kit = Kit.Crear(codigo, nombre);
        if (await _kits.ExisteCodigoAsync(kit.Codigo, ct) || await _productos.ExisteSkuAsync(kit.Codigo, ct))
            return ResultadoOperacion.Fallo("CODIGO_DUPLICADO", $"El código {kit.Codigo} ya existe.");
        if (componentes.Count == 0) return ResultadoOperacion.Fallo("KIT_VACIO", "El kit necesita al menos un componente.");

        foreach (var (cod, cant) in componentes) await AgregarAKitAsync(kit, cod, cant, ct);
        await ValidarArbolConAsync(kit, ct);

        _kits.Agregar(kit);
        Auditar("KIT_CREADO", $"{kit.Codigo}: {string.Join(", ", componentes.Select(c => $"{c.Codigo}x{c.Cantidad}"))}");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Kit {kit.Codigo} creado.");
    }

    public async Task<ResultadoOperacion> AgregarComponenteAsync(string codigoKit, string codigoComponente, int cantidad, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var kit = await _kits.ObtenerPorCodigoAsync(codigoKit, ct)
                  ?? throw new ExcepcionAplicacion("KIT_NO_EXISTE", $"No existe el kit {codigoKit}.");
        await AgregarAKitAsync(kit, codigoComponente, cantidad, ct);
        await ValidarArbolConAsync(kit, ct); // rechaza ciclos indirectos antes de guardar
        Auditar("KIT_MODIFICADO", $"{kit.Codigo} + {codigoComponente}x{cantidad}");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Componente agregado a {kit.Codigo}.");
    }

    public async Task<ResultadoOperacion> QuitarComponenteAsync(string codigoKit, string codigoComponente, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarCatalogo);
        var kit = await _kits.ObtenerPorCodigoAsync(codigoKit, ct)
                  ?? throw new ExcepcionAplicacion("KIT_NO_EXISTE", $"No existe el kit {codigoKit}.");
        if (kit.Componentes.Count <= 1)
            return ResultadoOperacion.Fallo("KIT_VACIO", "Un kit no puede quedar sin componentes.");
        kit.QuitarComponente(codigoComponente);
        Auditar("KIT_MODIFICADO", $"{kit.Codigo} - {codigoComponente}");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Componente {codigoComponente} quitado de {kit.Codigo}.");
    }

    private async Task AgregarAKitAsync(Kit kit, string codigo, int cantidad, CancellationToken ct)
    {
        var producto = await _productos.ObtenerPorSkuAsync(codigo, ct);
        if (producto is not null)
        {
            kit.AgregarComponente(producto, cantidad);
            return;
        }
        var hijo = await _kits.ObtenerPorCodigoAsync(codigo, ct)
                   ?? throw new ExcepcionAplicacion("CATALOGO_NO_EXISTE", $"El código {codigo} no existe.");
        kit.AgregarComponente(hijo, cantidad);
    }

    private async Task ValidarArbolConAsync(Kit kitModificado, CancellationToken ct)
    {
        var kits = (await _kits.ListarAsync(ct)).Where(k => k.Codigo != kitModificado.Codigo).Append(kitModificado);
        new ArbolCatalogo(await _productos.ListarAsync(ct), kits).ValidarIntegridad();
    }

    private void Auditar(string accion, string detalle) =>
        _auditoria.Agregar(new Auditoria(_reloj.Ahora, _sesion.NombreUsuario, "CATALOGO", accion, detalle));
}
