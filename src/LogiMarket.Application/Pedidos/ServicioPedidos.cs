using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

/// <summary>Casos de uso de borradores, validación, cancelación, clonación (Prototype) y consultas.</summary>
public sealed class ServicioPedidos
{
    private readonly IPedidoRepositorio _pedidos;
    private readonly IClienteRepositorio _clientes;
    private readonly IProductoRepositorio _productos;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IConsultasLectura _consultas;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly ISesionActual _sesion;
    private readonly ServicioValoracion _valoracion;
    private readonly CadenaValidacionPedido _cadena;
    private readonly IReloj _reloj;

    public ServicioPedidos(IPedidoRepositorio pedidos, IClienteRepositorio clientes, IProductoRepositorio productos,
        IAuditoriaRepositorio auditoria, IConsultasLectura consultas, IUnidadDeTrabajo uow,
        IServicioAutorizacion autorizacion, ISesionActual sesion, ServicioValoracion valoracion,
        CadenaValidacionPedido cadena, IReloj reloj)
    {
        _pedidos = pedidos;
        _clientes = clientes;
        _productos = productos;
        _auditoria = auditoria;
        _consultas = consultas;
        _uow = uow;
        _autorizacion = autorizacion;
        _sesion = sesion;
        _valoracion = valoracion;
        _cadena = cadena;
        _reloj = reloj;
    }

    // ---------------- Consultas ----------------

    public Task<IReadOnlyList<PedidoResumenDto>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _consultas.PedidosAsync(ct);
    }

    public async Task<PedidoCompletoDto> DetalleAsync(int id, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return await _consultas.PedidoAsync(id, ct) ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {id}.");
    }

    // ---------------- Borradores ----------------

    public EditorBorrador NuevoEditor()
    {
        _autorizacion.Exigir(Operacion.GestionarBorradores);
        return new EditorBorrador(_valoracion);
    }

    public async Task<EditorBorrador> AbrirEditorAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarBorradores);
        var pedido = await _pedidos.ObtenerAsync(pedidoId, ct)
                     ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {pedidoId}.");
        return new EditorBorrador(_valoracion, pedido);
    }

    /// <summary>Persiste el borrador del editor (alta o reemplazo de su contenido).</summary>
    public async Task<ResultadoOperacion<int>> GuardarBorradorAsync(EditorBorrador editor, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarBorradores);
        var borrador = editor.Pedido ?? throw new ExcepcionAplicacion("BORRADOR_VACIO", "El carrito está vacío.");
        if (borrador.ClienteId == 0) return ResultadoOperacion<int>.Fallo("PEDIDO_SIN_CLIENTE", "Seleccione un cliente.");
        _valoracion.Valorar(borrador);

        if (borrador.Id == 0)
        {
            _pedidos.Agregar(borrador);
        }
        else
        {
            var persistido = await _pedidos.ObtenerAsync(borrador.Id, ct)
                             ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {borrador.Id}.");
            if (!ReferenceEquals(persistido, borrador))
            {
                persistido.ReemplazarContenido(borrador);
                _valoracion.Valorar(persistido);
            }
        }
        await _uow.GuardarCambiosAsync(ct);
        editor.MarcarGuardado();
        return ResultadoOperacion<int>.Ok(borrador.Id, $"Borrador {borrador.Numero} guardado. Total USD {Dinero.Formatear(borrador.Total)}.");
    }

    // ---------------- Validación (Chain) ----------------

    public async Task<ResultadoValidacion> ValidarAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ValidarPedido);
        var pedido = await ObtenerAsync(pedidoId, ct);
        var resultado = await EvaluarCadenaAsync(pedido, ct);
        if (!resultado.EsValido) return resultado;

        _valoracion.Valorar(pedido);
        pedido.Validar(_reloj.Ahora, _sesion.NombreUsuario);
        Auditar("PEDIDO_VALIDADO", $"{pedido.Numero} total {Dinero.Formatear(pedido.Total)}");
        await _uow.GuardarCambiosAsync(ct);
        return resultado;
    }

    /// <summary>Evalúa la cadena sin cambiar el estado (diagnóstico para la UI y pruebas).</summary>
    public async Task<ResultadoValidacion> DiagnosticarAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return await EvaluarCadenaAsync(await ObtenerAsync(pedidoId, ct), ct);
    }

    private async Task<ResultadoValidacion> EvaluarCadenaAsync(Pedido pedido, CancellationToken ct)
    {
        var cliente = await _clientes.ObtenerAsync(pedido.ClienteId, ct);
        var requerimiento = pedido.RequerimientoPorSku();
        var stock = (await _productos.ObtenerPorSkusAsync(requerimiento.Keys, ct))
            .ToDictionary(p => p.Sku, p => p.Activo ? p.Stock : 0, StringComparer.OrdinalIgnoreCase);
        return _cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = stock, Usuario = _sesion.Usuario });
    }

    // ---------------- Cancelación (State) ----------------

    public async Task<ResultadoOperacion> CancelarAsync(int pedidoId, string motivo, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.CancelarPedido);
        var pedido = await ObtenerAsync(pedidoId, ct);
        pedido.Cancelar(_reloj.Ahora, _sesion.NombreUsuario, string.IsNullOrWhiteSpace(motivo) ? "Sin motivo" : motivo.Trim());
        Auditar("PEDIDO_CANCELADO", $"{pedido.Numero}: {motivo}");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Pedido {pedido.Numero} cancelado.");
    }

    // ---------------- Prototype ----------------

    /// <summary>Clona un pedido existente (plantilla) como un BORRADOR nuevo sin id, pago ni historial.</summary>
    public async Task<ResultadoOperacion<int>> ClonarComoBorradorAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarBorradores);
        var plantilla = await ObtenerAsync(pedidoId, ct);
        var copia = plantilla.ClonarComoBorrador(_reloj.Ahora, _sesion.NombreUsuario);
        _valoracion.Valorar(copia);
        _pedidos.Agregar(copia);
        Auditar("PEDIDO_CLONADO", $"Plantilla {plantilla.Numero} → nuevo borrador");
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion<int>.Ok(copia.Id, $"Se creó el borrador {copia.Numero} a partir de {plantilla.Numero}.");
    }

    private async Task<Pedido> ObtenerAsync(int id, CancellationToken ct) =>
        await _pedidos.ObtenerAsync(id, ct) ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {id}.");

    private void Auditar(string accion, string detalle) =>
        _auditoria.Agregar(new Auditoria(_reloj.Ahora, _sesion.NombreUsuario, "NEGOCIO", accion, detalle));
}
