using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Consultas;

//LADO DE LECTURA (CQRS basico): reune todas las consultas
//solo lee mediante IConsultasLectura (AsNoTracking) y nunca guarda cambios
//los servicios de comando no exponen consultas
public sealed class ServicioConsultas : IServicioConsultas
{
    private readonly IConsultasLectura _lectura;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly IBitacoraTecnica _bitacora;
    private readonly IMetricasSesion _metricas;
    private readonly IInvocadorComandos _invocador;

    public ServicioConsultas(IConsultasLectura lectura, IServicioAutorizacion autorizacion, IBitacoraTecnica bitacora,
        IMetricasSesion metricas, IInvocadorComandos invocador)
    {
        _lectura = lectura;
        _autorizacion = autorizacion;
        _bitacora = bitacora;
        _metricas = metricas;
        _invocador = invocador;
    }

    //catalogo

    public Task<IReadOnlyList<ProductoDto>> ProductosAsync(string? texto = null, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _lectura.ProductosAsync(texto, ct);
    }

    public async Task<IReadOnlyList<KitDto>> KitsAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var catalogo = await _lectura.CatalogoAsync(ct);
        var stock = catalogo.Productos.ToDictionary(p => p.Sku, p => p.Stock, StringComparer.OrdinalIgnoreCase);
        var arbol = new ArbolCatalogo(catalogo.Productos, catalogo.Kits);
        var nombres = catalogo.Productos.Select(p => (Codigo: p.Sku, p.Nombre))
            .Concat(catalogo.Kits.Select(k => (k.Codigo, k.Nombre)))
            .ToDictionary(x => x.Codigo, x => x.Nombre, StringComparer.OrdinalIgnoreCase);

        return catalogo.Kits.Select(k =>
        {
            var elemento = arbol.Obtener(k.Codigo);
            var componentes = k.Componentes.Select(c =>
                new ComponenteKitDto(c.CodigoComponente, nombres.GetValueOrDefault(c.CodigoComponente, "?"), c.EsKit, c.Cantidad)).ToList();
            return new KitDto(k.Id, k.Codigo, k.Nombre, elemento.Precio, elemento.PesoKg, UnidadesVendibles(elemento, stock),
                componentes, string.Join(" + ", componentes.Select(c => $"{c.Codigo}x{c.Cantidad}")));
        }).ToList();
    }

    public async Task<IReadOnlyList<ElementoVendibleDto>> VendiblesAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var catalogo = await _lectura.CatalogoAsync(ct);
        var stock = catalogo.Productos.ToDictionary(p => p.Sku, p => p.Activo ? p.Stock : 0, StringComparer.OrdinalIgnoreCase);
        var arbol = new ArbolCatalogo(catalogo.Productos, catalogo.Kits);
        var lista = catalogo.Productos.Where(p => p.Activo)
            .Select(p => new ElementoVendibleDto(p.Sku, p.Nombre, false, p.Precio, p.Stock)).ToList();
        foreach (var codigo in arbol.CodigosKits.OrderBy(c => c))
        {
            var e = arbol.Obtener(codigo);
            lista.Add(new ElementoVendibleDto(e.Codigo, e.Nombre, true, e.Precio, UnidadesVendibles(e, stock)));
        }
        return lista;
    }

    //elemento del Composite (instantanea de precios y composicion) para agregar al carrito
    public async Task<IElementoCatalogo> ElementoAsync(string codigo, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var catalogo = await _lectura.CatalogoAsync(ct);
        return new ArbolCatalogo(catalogo.Productos, catalogo.Kits).Obtener(codigo);
    }

    //recorre un elemento con el Iterator y con los dos Visitors
    public async Task<DesgloseDto> DesgloseAsync(string codigo, CancellationToken ct = default)
    {
        var elemento = await ElementoAsync(codigo, ct);
        var desglose = new DesgloseSkuVisitor();
        var peso = new PesoLogisticoVisitor();
        elemento.Aceptar(desglose);
        elemento.Aceptar(peso);
        return new DesgloseDto(elemento.Codigo, elemento.Precio, peso.PesoTotalKg, elemento.ComponentesFisicos(),
            desglose.Lineas, desglose.KitsVisitados, peso.Resumen);
    }

    //unidades vendibles de un elemento segun el stock de sus SKU finales (usa el Iterator)
    private static int UnidadesVendibles(IElementoCatalogo elemento, IReadOnlyDictionary<string, int> stock) =>
        elemento.ExpandirSkus().Select(kv => stock.GetValueOrDefault(kv.Key) / kv.Value).DefaultIfEmpty(0).Min();

    //clientes

    public Task<IReadOnlyList<ClienteDto>> ClientesAsync(string? texto = null, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _lectura.ClientesAsync(texto, ct);
    }

    public async Task<ClienteDto> ClienteAsync(int id, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return await _lectura.ClienteAsync(id, ct) ?? throw new ExcepcionAplicacion("CLIENTE_NO_EXISTE", $"No existe el cliente {id}.");
    }

    //pedidos y logistica

    public Task<IReadOnlyList<PedidoResumenDto>> PedidosAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _lectura.PedidosAsync(ct);
    }

    public async Task<PedidoCompletoDto> PedidoAsync(int id, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return await _lectura.PedidoAsync(id, ct) ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {id}.");
    }

    public async Task<string> SeguimientoAsync(string guiaOPedido, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var texto = guiaOPedido.Trim();
        var despacho = int.TryParse(texto.Replace("PED-", "", StringComparison.OrdinalIgnoreCase), out var id)
            ? (await _lectura.PedidoAsync(id, ct))?.Despacho
            : await _lectura.DespachoPorGuiaAsync(texto.ToUpperInvariant(), ct);
        return despacho is null
            ? $"No se encontró seguimiento para '{texto}'."
            : $"Guía {despacho.NumeroGuia} (PED-{despacho.PedidoId:000000}): {despacho.Seguimiento}";
    }

    //operacion y auditoria

    public Task<IndicadoresDto> IndicadoresAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _lectura.IndicadoresAsync(ParametrosNegocio.UmbralStockBajo, ct);
    }

    public Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _lectura.MovimientosAsync(sku, ct);
    }

    public Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(string? categoria, string? texto, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _lectura.AuditoriaAsync(categoria, texto, ct);
    }

    public Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _lectura.OutboxAsync(ct);
    }

    public IReadOnlyList<EntradaBitacora> Bitacora(string? categoria)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _bitacora.Entradas(categoria).Reverse().ToList();
    }

    public IReadOnlyList<RegistroComando> HistorialComandos()
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _invocador.Historial.Reverse().ToList();
    }

    public InstantaneaMetricas Metricas() => _metricas.Instantanea();
}
