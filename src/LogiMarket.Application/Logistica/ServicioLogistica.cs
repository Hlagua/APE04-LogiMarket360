using LogiMarket.Application.Comun;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Logistica;

namespace LogiMarket.Application.Logistica;

//comandos de logistica: despacho (guia con la familia del Abstract Factory) y entrega
//el seguimiento es una consulta (ServicioConsultas)
public sealed class ServicioLogistica : IServicioLogistica
{
    private readonly IPedidoRepositorio _pedidos;
    private readonly IDespachoRepositorio _despachos;
    private readonly IOutboxRepositorio _outbox;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly ISesionActual _sesion;
    private readonly IProveedorFabricasEntrega _fabricas;
    private readonly IProcesadorOutbox _procesadorOutbox;
    private readonly IMetricasSesion _metricas;
    private readonly IReloj _reloj;

    public ServicioLogistica(IPedidoRepositorio pedidos, IDespachoRepositorio despachos, IOutboxRepositorio outbox,
        IAuditoriaRepositorio auditoria, IUnidadDeTrabajo uow, IServicioAutorizacion autorizacion, ISesionActual sesion,
        IProveedorFabricasEntrega fabricas, IProcesadorOutbox procesadorOutbox, IMetricasSesion metricas, IReloj reloj)
    {
        _pedidos = pedidos;
        _despachos = despachos;
        _outbox = outbox;
        _auditoria = auditoria;
        _uow = uow;
        _autorizacion = autorizacion;
        _sesion = sesion;
        _fabricas = fabricas;
        _procesadorOutbox = procesadorOutbox;
        _metricas = metricas;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion<string>> DespacharAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.Despachar);
        var guia = await _uow.EnTransaccionAsync(async c =>
        {
            var ahora = _reloj.Ahora;
            var pedido = await _pedidos.ObtenerAsync(pedidoId, c)
                         ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {pedidoId}.");

            //abstract Factory: plazo y guia de la MISMA familia que el pedido
            var fabrica = _fabricas.Para(pedido.TipoEntrega);
            var plazo = fabrica.CrearCalculadoraPlazo();
            var guiaEnvio = fabrica.CrearGeneradorGuia().Generar(pedido.Id, pedido.ClienteNombre, pedido.PesoKg, ahora, plazo);

            pedido.Despachar(ahora, _sesion.NombreUsuario, guiaEnvio.Numero); //state: solo desde PAGADO
            _despachos.Agregar(new Despacho(pedido.Id, guiaEnvio.Numero, guiaEnvio.Familia, guiaEnvio.PlazoDias,
                guiaEnvio.Etiqueta, ahora, guiaEnvio.FechaEstimada, _sesion.NombreUsuario));
            _outbox.Agregar(SerializadorEventos.AOutbox(new DespachoRegistrado(pedido.Id, pedido.ClienteNombre,
                pedido.ClienteEmail, pedido.ClienteTelefono, ahora, guiaEnvio.Numero, guiaEnvio.Familia)));
            _auditoria.Agregar(new Auditoria(ahora, _sesion.NombreUsuario, "NEGOCIO", "PEDIDO_DESPACHADO",
                $"{pedido.Numero} guía {guiaEnvio.Numero} ({guiaEnvio.Familia}, {guiaEnvio.PlazoDias} días)"));
            await _uow.GuardarCambiosAsync(c);
            return guiaEnvio;
        }, ct);

        _metricas.RegistrarDespacho();
        await _procesadorOutbox.ProcesarSinPropagarAsync(ct);
        return ResultadoOperacion<string>.Ok(guia.Numero, $"Pedido despachado. Guía {guia.Numero}. {guia.Etiqueta}");
    }

    public async Task<ResultadoOperacion> EntregarAsync(int pedidoId, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.Entregar);
        await _uow.EnTransaccionAsync(async c =>
        {
            var ahora = _reloj.Ahora;
            var pedido = await _pedidos.ObtenerAsync(pedidoId, c)
                         ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {pedidoId}.");
            pedido.Entregar(ahora, _sesion.NombreUsuario); //state: solo desde DESPACHADO
            var despacho = await _despachos.ObtenerPorPedidoAsync(pedidoId, c)
                           ?? throw new ExcepcionAplicacion("SIN_DESPACHO", "El pedido no tiene despacho registrado.");
            despacho.RegistrarEntrega(ahora);
            _auditoria.Agregar(new Auditoria(ahora, _sesion.NombreUsuario, "NEGOCIO", "PEDIDO_ENTREGADO",
                $"{pedido.Numero} guía {despacho.NumeroGuia}"));
            await _uow.GuardarCambiosAsync(c);
            return true;
        }, ct);
        _metricas.RegistrarEntrega();
        return ResultadoOperacion.Ok($"Pedido {pedidoId} entregado.");
    }
}
