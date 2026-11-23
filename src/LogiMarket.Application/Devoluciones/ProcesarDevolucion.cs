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

/// <summary>Colaboradores comunes de todas las variantes de devolución.</summary>
public sealed record DependenciasDevolucion(IPedidoRepositorio Pedidos, IProductoRepositorio Productos,
    IDevolucionRepositorio Devoluciones, IPagoRepositorio Pagos, IMovimientoInventarioRepositorio Movimientos,
    IOutboxRepositorio Outbox, IAuditoriaRepositorio Auditoria, IUnidadDeTrabajo Uow, IServicioReembolso Reembolso,
    ISesionActual Sesion, IReloj Reloj, IProcesadorOutbox ProcesadorOutbox, IMetricasSesion Metricas);

/// <summary>
/// Patrón Template Method. <see cref="EjecutarAsync"/> fija la secuencia invariante:
/// verificar elegibilidad → inspeccionar condición → calcular reembolso → registrar devolución/stock →
/// generar evento. Las variantes (Defecto, Arrepentimiento) solo personalizan los pasos permitidos.
/// </summary>
public abstract class ProcesarDevolucion
{
    private readonly DependenciasDevolucion _d;

    protected ProcesarDevolucion(DependenciasDevolucion dependencias) => _d = dependencias;

    public abstract MotivoDevolucion Motivo { get; }

    /// <summary>Días máximos desde la entrega (gancho obligatorio).</summary>
    protected abstract int DiasMaximos { get; }

    /// <summary>Paso 2 (gancho obligatorio): condición física del producto.</summary>
    protected abstract string? InspeccionarCondicion(SolicitudDevolucion solicitud);

    /// <summary>Paso 3 (gancho opcional): por defecto, productos después del descuento y sin envío.</summary>
    protected virtual decimal CalcularReembolso(Pedido pedido) => pedido.ImporteReembolsable;

    /// <summary>Gancho opcional: ¿se reintegran las existencias?</summary>
    protected virtual bool ReintegraStock => true;

    /// <summary>MÉTODO PLANTILLA (no virtual): la estructura no cambia entre variantes.</summary>
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

                // 1) Elegibilidad (común + plazo de la variante)
                pasos.Add("1-VerificarElegibilidad");
                var dias = VerificarElegibilidad(pedido, ahora);
                if (await _d.Devoluciones.ObtenerPorPedidoAsync(pedido.Id, c) is not null)
                    throw new ExcepcionDominio("DEVOLUCION_DUPLICADA", $"El pedido {pedido.Numero} ya tiene una devolución.");
                if (dias > DiasMaximos)
                    throw new ExcepcionDominio("FUERA_DE_PLAZO",
                        $"{Motivo}: han pasado {dias} días desde la entrega; el máximo es {DiasMaximos}.");

                // 2) Inspección (variante)
                pasos.Add("2-InspeccionarCondicion");
                var problema = InspeccionarCondicion(solicitud);
                if (problema is not null) throw new ExcepcionDominio("CONDICION_NO_ACEPTADA", problema);

                // 3) Reembolso (variante opcional)
                pasos.Add("3-CalcularReembolso");
                var monto = Dinero.Redondear(CalcularReembolso(pedido));

                // 4) Registro de devolución, stock y reembolso simulado único
                pasos.Add("4-RegistrarDevolucionYStock");
                var pago = await _d.Pagos.ObtenerPorPedidoAsync(pedido.Id, c)
                           ?? throw new ExcepcionAplicacion("SIN_PAGO", "El pedido no registra un pago.");
                if (ReintegraStock) await ReintegrarAsync(pedido, ahora, usuario, c);
                var referencia = await _d.Reembolso.ReembolsarAsync(pedido.Id, monto, pago.Referencia, c);
                pedido.MarcarDevuelto(ahora, usuario, $"{Motivo} reembolso {Dinero.Formatear(monto)}");
                _d.Devoluciones.Agregar(new Devolucion(pedido.Id, Motivo, ahora, dias, monto, referencia, ReintegraStock,
                    solicitud.Observacion, usuario));

                // 5) Evento (Outbox en la misma transacción)
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
        pedido.ExigirDevolucionPosible(); // ENTREGADO y no devuelto
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

/// <summary>DEFECTO: hasta 30 días desde la entrega; no exige que el producto esté sin uso.</summary>
public sealed class ProcesarDevolucionDefecto : ProcesarDevolucion
{
    public ProcesarDevolucionDefecto(DependenciasDevolucion d) : base(d) { }
    public override MotivoDevolucion Motivo => MotivoDevolucion.DEFECTO;
    protected override int DiasMaximos => ParametrosNegocio.DiasMaximosDefecto;
    protected override string? InspeccionarCondicion(SolicitudDevolucion solicitud) => null;
}

/// <summary>ARREPENTIMIENTO: hasta 7 días y solo si el producto está sin uso.</summary>
public sealed class ProcesarDevolucionArrepentimiento : ProcesarDevolucion
{
    public ProcesarDevolucionArrepentimiento(DependenciasDevolucion d) : base(d) { }
    public override MotivoDevolucion Motivo => MotivoDevolucion.ARREPENTIMIENTO;
    protected override int DiasMaximos => ParametrosNegocio.DiasMaximosArrepentimiento;

    protected override string? InspeccionarCondicion(SolicitudDevolucion solicitud) =>
        solicitud.ProductoSinUso ? null : "Por arrepentimiento solo se aceptan productos sin uso.";
}

/// <summary>Punto de entrada protegido: elige la variante del Template Method según el motivo.</summary>
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
