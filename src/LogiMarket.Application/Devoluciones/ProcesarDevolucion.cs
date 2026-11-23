using LogiMarket.Application.Comun;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Devoluciones;

public sealed record SolicitudDevolucion(int PedidoId, MotivoDevolucion Motivo, bool ProductoSinUso, string Observacion);

public sealed class ResultadoDevolucion : ResultadoOperacion
{
    public decimal MontoReembolso { get; init; }
    public string? ReferenciaReembolso { get; init; }
    public bool StockReintegrado { get; init; }
    public int DiasDesdeEntrega { get; init; }
    public IReadOnlyList<string> Pasos { get; init; } = Array.Empty<string>();
}

//colaboradores comunes de todas las variantes de devolucion
public sealed record DependenciasDevolucion(IPedidoRepositorio Pedidos, IProductoRepositorio Productos,
    IDevolucionRepositorio Devoluciones, IPagoRepositorio Pagos, IMovimientoInventarioRepositorio Movimientos,
    IOutboxRepositorio Outbox, IAuditoriaRepositorio Auditoria, IUnidadDeTrabajo Uow, IServicioReembolso Reembolso,
    ISesionActual Sesion, IReloj Reloj, IProcesadorOutbox ProcesadorOutbox, IMetricasSesion Metricas);

//patron template method (comportamental)
//clase ProcesarDevolucion
//objetivo: EjecutarAsync fija la secuencia invariante: verificar elegibilidad -> inspeccionar condicion ->
//calcular reembolso -> registrar devolucion/stock -> generar evento
//las variantes (Defecto, Arrepentimiento) solo personalizan los pasos permitidos
public abstract class ProcesarDevolucion
{
    private readonly DependenciasDevolucion _d;

    protected ProcesarDevolucion(DependenciasDevolucion dependencias) => _d = dependencias;

    public abstract MotivoDevolucion Motivo { get; }

    //dias maximos desde la entrega (gancho obligatorio)
    protected abstract int DiasMaximos { get; }

    //paso 2 (gancho obligatorio): condicion fisica del producto
    protected abstract string? InspeccionarCondicion(SolicitudDevolucion solicitud);

    //paso 3 (gancho opcional): por defecto, productos despues del descuento y sin envio
    protected virtual decimal CalcularReembolso(Pedido pedido) => pedido.ImporteReembolsable;

    //gancho opcional: se reintegran las existencias?
    protected virtual bool ReintegraStock => true;

    //METODO PLANTILLA (no virtual): la estructura no cambia entre variantes
    public async Task<ResultadoDevolucion> EjecutarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        var pasos = new List<string>();
        var ahora = _d.Reloj.Ahora;
        var usuario = _d.Sesion.NombreUsuario;
        try
        {
            var resultado = await _d.Uow.EnTransaccionAsync(async c =>
            {
                var pedido = await _d.Pedidos.ObtenerAsync(solicitud.PedidoId, c)
                             ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {solicitud.PedidoId}.");

                //paso 1: elegibilidad (comun + plazo de la variante)
                pasos.Add("1-VerificarElegibilidad");
                var dias = VerificarElegibilidad(pedido, ahora);
                if (await _d.Devoluciones.ObtenerPorPedidoAsync(pedido.Id, c) is not null)
                    throw new ExcepcionDominio("DEVOLUCION_DUPLICADA", $"El pedido {pedido.Numero} ya tiene una devolución.");
                if (dias > DiasMaximos)
                    throw new ExcepcionDominio("FUERA_DE_PLAZO",
                        $"{Motivo}: han pasado {dias} días desde la entrega; el máximo es {DiasMaximos}.");

                //paso 2: inspeccion (variante)
                pasos.Add("2-InspeccionarCondicion");
                var problema = InspeccionarCondicion(solicitud);
                if (problema is not null) throw new ExcepcionDominio("CONDICION_NO_ACEPTADA", problema);

                //paso 3: reembolso (variante opcional)
                pasos.Add("3-CalcularReembolso");
                var monto = Dinero.Redondear(CalcularReembolso(pedido));

                //paso 4: registro de devolucion, stock y reembolso simulado unico
                pasos.Add("4-RegistrarDevolucionYStock");
                var pago = await _d.Pagos.ObtenerPorPedidoAsync(pedido.Id, c)
                           ?? throw new ExcepcionAplicacion("SIN_PAGO", "El pedido no registra un pago.");
                if (ReintegraStock) await ReintegrarAsync(pedido, ahora, usuario, c);
                var referencia = await _d.Reembolso.ReembolsarAsync(pedido.Id, monto, pago.Referencia, c);
                pedido.MarcarDevuelto(ahora, usuario, $"{Motivo} reembolso {Dinero.Formatear(monto)}");
                _d.Devoluciones.Agregar(new Devolucion(pedido.Id, Motivo, ahora, dias, monto, referencia, ReintegraStock,
                    solicitud.Observacion, usuario));

                //paso 5: evento (Outbox en la misma transaccion)
                pasos.Add("5-GenerarEvento");
                _d.Outbox.Agregar(SerializadorEventos.AOutbox(new DevolucionAceptada(pedido.Id, pedido.ClienteNombre,
                    pedido.ClienteEmail, pedido.ClienteTelefono, ahora, monto, Motivo.ToString())));
                _d.Auditoria.Agregar(new Auditoria(ahora, usuario, "NEGOCIO", "DEVOLUCION_ACEPTADA",
                    $"{pedido.Numero} {Motivo} día {dias}: reembolso {Dinero.Formatear(monto)} ref {referencia}"));
                await _d.Uow.GuardarCambiosAsync(c);

                return new ResultadoDevolucion
                {
                    Exito = true, Codigo = "OK", MontoReembolso = monto, ReferenciaReembolso = referencia,
                    StockReintegrado = ReintegraStock, DiasDesdeEntrega = dias, Pasos = pasos,
                    Mensaje = $"Devolución {Motivo} aceptada: reembolso USD {Dinero.Formatear(monto)} (sin tarifa de envío)."
                };
            }, ct);

            _d.Metricas.RegistrarDevolucion();
            await _d.ProcesadorOutbox.ProcesarSinPropagarAsync(ct);
            return resultado;
        }
        catch (Exception ex) when (ex is ExcepcionDominio or ExcepcionAplicacion)
        {
            var codigo = ex is ExcepcionDominio d ? d.Codigo : ((ExcepcionAplicacion)ex).Codigo;
            _d.Uow.DescartarCambios();
            _d.Auditoria.Agregar(new Auditoria(ahora, usuario, "RECHAZO", "DEVOLUCION_RECHAZADA",
                $"Pedido {solicitud.PedidoId} {Motivo}: [{codigo}] {ex.Message}"));
            await _d.Uow.GuardarCambiosAsync(ct);
            return new ResultadoDevolucion { Exito = false, Codigo = codigo, Mensaje = ex.Message, Pasos = pasos };
        }
    }

    private static int VerificarElegibilidad(Pedido pedido, DateTime ahora)
    {
        pedido.ExigirDevolucionPosible(); //ENTREGADO y no devuelto
        return (int)Math.Floor((ahora.Date - pedido.FechaEntrega!.Value.Date).TotalDays);
    }

    private async Task ReintegrarAsync(Pedido pedido, DateTime ahora, string usuario, CancellationToken ct)
    {
        var requerimiento = pedido.RequerimientoPorSku();
        var productos = await _d.Productos.ObtenerPorSkusAsync(requerimiento.Keys, ct);
        foreach (var producto in productos)
        {
            var cantidad = requerimiento[producto.Sku];
            producto.ReintegrarStock(cantidad);
            _d.Movimientos.Agregar(new MovimientoInventario(producto.Id, producto.Sku, cantidad, producto.Stock,
                MotivoMovimiento.DEVOLUCION, $"{pedido.Numero} {Motivo}", ahora, usuario));
        }
    }
}

//DEFECTO: hasta 30 dias desde la entrega
//no exige que el producto este sin uso
public sealed class ProcesarDevolucionDefecto : ProcesarDevolucion
{
    public ProcesarDevolucionDefecto(DependenciasDevolucion d) : base(d) { }
    public override MotivoDevolucion Motivo => MotivoDevolucion.DEFECTO;
    protected override int DiasMaximos => ParametrosNegocio.DiasMaximosDefecto;
    protected override string? InspeccionarCondicion(SolicitudDevolucion solicitud) => null;
}

//ARREPENTIMIENTO: hasta 7 dias y solo si el producto esta sin uso
public sealed class ProcesarDevolucionArrepentimiento : ProcesarDevolucion
{
    public ProcesarDevolucionArrepentimiento(DependenciasDevolucion d) : base(d) { }
    public override MotivoDevolucion Motivo => MotivoDevolucion.ARREPENTIMIENTO;
    protected override int DiasMaximos => ParametrosNegocio.DiasMaximosArrepentimiento;

    protected override string? InspeccionarCondicion(SolicitudDevolucion solicitud) =>
        solicitud.ProductoSinUso ? null : "Por arrepentimiento solo se aceptan productos sin uso.";
}

//punto de entrada protegido: elige la variante del Template Method segun el motivo
public sealed class ServicioDevoluciones : IServicioDevoluciones
{
    private readonly IServicioAutorizacion _autorizacion;
    private readonly IReadOnlyDictionary<MotivoDevolucion, ProcesarDevolucion> _variantes;

    public ServicioDevoluciones(IServicioAutorizacion autorizacion, IEnumerable<ProcesarDevolucion> variantes)
    {
        _autorizacion = autorizacion;
        _variantes = variantes.ToDictionary(v => v.Motivo);
    }

    public Task<ResultadoDevolucion> SolicitarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ProcesarDevolucion);
        return _variantes[solicitud.Motivo].EjecutarAsync(solicitud, ct);
    }
}
