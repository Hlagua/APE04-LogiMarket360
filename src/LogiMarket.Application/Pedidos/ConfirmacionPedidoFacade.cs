using LogiMarket.Application.Comun;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

public sealed class ResultadoConfirmacion : ResultadoOperacion
{
    public int PedidoId { get; init; }
    public decimal Total { get; init; }
    public string? ReferenciaPago { get; init; }
    public bool EsRepeticion { get; init; }
    public RechazoValidacion? Rechazo { get; init; }
}

/// <summary>Colaboradores de persistencia agrupados (evita un constructor inmanejable).</summary>
public sealed record RepositoriosConfirmacion(IPedidoRepositorio Pedidos, IClienteRepositorio Clientes,
    IProductoRepositorio Productos, IPagoRepositorio Pagos, IMovimientoInventarioRepositorio Movimientos,
    IOutboxRepositorio Outbox, IAuditoriaRepositorio Auditoria);

/// <summary>
/// Patrón Facade: UNA operación de confirmación que coordina autorización, validación (Chain),
/// valoración (Strategy + Abstract Factory), cobro (Proxy → Decorator → Adapter), stock, persistencia y
/// Outbox en una sola transacción. No contiene SQL ni reglas de descuento: delega en sus colaboradores.
/// </summary>
public sealed class ConfirmacionPedidoFacade
{
    private readonly IServicioAutorizacion _autorizacion;
    private readonly ISesionActual _sesion;
    private readonly RepositoriosConfirmacion _repos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IPasarelaPago _pasarela;
    private readonly ServicioValoracion _valoracion;
    private readonly CadenaValidacionPedido _cadena;
    private readonly ProcesadorOutbox _procesadorOutbox;
    private readonly MetricasSesion _metricas;
    private readonly IReloj _reloj;

    public ConfirmacionPedidoFacade(IServicioAutorizacion autorizacion, ISesionActual sesion, RepositoriosConfirmacion repos,
        IUnidadDeTrabajo uow, IPasarelaPago pasarela, ServicioValoracion valoracion, CadenaValidacionPedido cadena,
        ProcesadorOutbox procesadorOutbox, MetricasSesion metricas, IReloj reloj)
    {
        _autorizacion = autorizacion;
        _sesion = sesion;
        _repos = repos;
        _uow = uow;
        _pasarela = pasarela;
        _valoracion = valoracion;
        _cadena = cadena;
        _procesadorOutbox = procesadorOutbox;
        _metricas = metricas;
        _reloj = reloj;
    }

    public async Task<ResultadoConfirmacion> ConfirmarAsync(int pedidoId, string claveIdempotencia, CancellationToken ct = default)
    {
        try
        {
            _autorizacion.Exigir(Operacion.ConfirmarPedido);

            var repetido = await ResultadoPreviaAsync(pedidoId, claveIdempotencia, ct);
            if (repetido is not null) return repetido;

            var resultado = await _uow.EnTransaccionAsync(c => ConfirmarEnTransaccionAsync(pedidoId, claveIdempotencia, c), ct);

            _metricas.RegistrarConfirmacion(resultado.Total);
            await _procesadorOutbox.ProcesarSinPropagarAsync(ct); // postconfirmación: Observer
            return resultado;
        }
        catch (ExcepcionValidacionPedido ex)
        {
            return await RechazarAsync(pedidoId, ex.Rechazo.Codigo, ex.Message, ex.Rechazo, ct);
        }
        catch (AccesoDenegadoException ex)
        {
            // La denegación ya la contó quien la originó (ServicioAutorizacion o PagoAutorizadoProxy).
            return await RechazarAsync(pedidoId, "ACCESO_DENEGADO", ex.Message, null, ct, "SEGURIDAD");
        }
        catch (ExcepcionConcurrencia ex)
        {
            // Otra transacción ganó la carrera: si fue con la misma clave, se devuelve su resultado.
            var repetido = await ResultadoPreviaAsync(pedidoId, claveIdempotencia, ct);
            return repetido ?? await RechazarAsync(pedidoId, ex.Codigo, "Conflicto de concurrencia: el stock o el pedido cambió. " + ex.Message, null, ct);
        }
        catch (ExcepcionCobroRechazado ex)
        {
            _metricas.RegistrarCobroRechazado();
            return await RechazarAsync(pedidoId, ex.Codigo, ex.Message, null, ct);
        }
        catch (ExcepcionAplicacion ex)
        {
            return await RechazarAsync(pedidoId, ex.Codigo, ex.Message, null, ct);
        }
        catch (ExcepcionDominio ex)
        {
            return await RechazarAsync(pedidoId, ex.Codigo, ex.Message, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metricas.RegistrarCobroRechazado();
            return await RechazarAsync(pedidoId, "ERROR_COLABORADOR", $"Fallo de un colaborador: {ex.Message}", null, ct);
        }
    }

    private async Task<ResultadoConfirmacion> ConfirmarEnTransaccionAsync(int pedidoId, string clave, CancellationToken ct)
    {
        var ahora = _reloj.Ahora;
        var usuario = _sesion.NombreUsuario;

        var pedido = await _repos.Pedidos.ObtenerAsync(pedidoId, ct)
                     ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {pedidoId}.");
        var cliente = await _repos.Clientes.ObtenerAsync(pedido.ClienteId, ct);
        var requerimiento = pedido.RequerimientoPorSku();
        var productos = (await _repos.Productos.ObtenerPorSkusAsync(requerimiento.Keys, ct))
            .ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);

        // 1) Chain of Responsibility
        var validacion = _cadena.Validar(new ContextoValidacion
        {
            Pedido = pedido,
            Cliente = cliente,
            StockDisponible = productos.ToDictionary(p => p.Key, p => p.Value.Activo ? p.Value.Stock : 0, StringComparer.OrdinalIgnoreCase),
            Usuario = _sesion.Usuario
        });
        if (!validacion.EsValido) throw new ExcepcionValidacionPedido(validacion.Rechazo!);

        // 2) Valoración (Strategy + Abstract Factory) y transición BORRADOR → VALIDADO si corresponde
        _valoracion.Valorar(pedido);
        if (pedido.Estado == EstadoPedido.BORRADOR) pedido.Validar(ahora, usuario);

        // 3) Stock: una sola vez, por SKU final, con movimiento negativo trazable
        foreach (var (sku, cantidad) in requerimiento)
        {
            var producto = productos[sku];
            producto.DescontarStock(cantidad);
            _repos.Movimientos.Agregar(new MovimientoInventario(producto.Id, sku, -cantidad, producto.Stock,
                MotivoMovimiento.VENTA, pedido.Numero, ahora, usuario));
        }

        // 4) State: VALIDADO → PAGADO (rechaza estados inválidos) y cobro protegido
        pedido.RegistrarPago(clave, ahora, usuario);
        var cobro = await _pasarela.CobrarAsync(pedido.Total, clave, ct);
        if (!cobro.Aprobado) throw new ExcepcionCobroRechazado($"Cobro rechazado por {cobro.Proveedor}: {cobro.Mensaje}");

        // 5) Pago + Outbox + auditoría en la MISMA transacción
        _repos.Pagos.Agregar(Pago.Aprobado(pedido.Id, cobro.Referencia, clave, pedido.Total, cobro.Proveedor, cobro.CorrelacionId, ahora));
        _repos.Outbox.Agregar(SerializadorEventos.AOutbox(new PedidoPagado(pedido.Id, pedido.ClienteNombre,
            pedido.ClienteEmail, pedido.ClienteTelefono, ahora, pedido.Total, cobro.Referencia)));
        _repos.Auditoria.Agregar(new Auditoria(ahora, usuario, "NEGOCIO", "PEDIDO_CONFIRMADO",
            $"{pedido.Numero} total {Dinero.Formatear(pedido.Total)} política {pedido.PoliticaDescuento} ref {cobro.Referencia}"));

        await _uow.GuardarCambiosAsync(ct);

        return new ResultadoConfirmacion
        {
            Exito = true,
            Codigo = "OK",
            Mensaje = $"Pedido {pedido.Numero} PAGADO. Total USD {Dinero.Formatear(pedido.Total)} (ref. {cobro.Referencia}).",
            PedidoId = pedido.Id,
            Total = pedido.Total,
            ReferenciaPago = cobro.Referencia
        };
    }

    /// <summary>Idempotencia: si ya existe un pago con esa clave, se devuelve el resultado original.</summary>
    private async Task<ResultadoConfirmacion?> ResultadoPreviaAsync(int pedidoId, string clave, CancellationToken ct)
    {
        _uow.DescartarCambios();
        var pago = await _repos.Pagos.ObtenerPorClaveAsync(clave, ct);
        if (pago is null) return null;
        if (pago.PedidoId != pedidoId)
            return new ResultadoConfirmacion
            {
                Exito = false, Codigo = "CLAVE_REUTILIZADA", PedidoId = pedidoId,
                Mensaje = "La clave de idempotencia ya se usó para otro pedido."
            };
        _metricas.RegistrarConfirmacionRepetida();
        return new ResultadoConfirmacion
        {
            Exito = true,
            Codigo = "YA_CONFIRMADO",
            EsRepeticion = true,
            PedidoId = pedidoId,
            Total = pago.Monto,
            ReferenciaPago = pago.Referencia,
            Mensaje = $"Confirmación repetida: se devuelve el resultado original (ref. {pago.Referencia}); no hubo un nuevo cobro."
        };
    }

    private async Task<ResultadoConfirmacion> RechazarAsync(int pedidoId, string codigo, string mensaje,
        RechazoValidacion? rechazo, CancellationToken ct, string categoria = "RECHAZO")
    {
        // La transacción ya se revirtió; la auditoría del rechazo se guarda aparte.
        _uow.DescartarCambios();
        try
        {
            _repos.Auditoria.Agregar(new Auditoria(_reloj.Ahora, _sesion.NombreUsuario, categoria,
                "CONFIRMACION_RECHAZADA", $"Pedido {pedidoId}: [{codigo}] {mensaje}"));
            await _uow.GuardarCambiosAsync(ct);
        }
        catch (Exception)
        {
            _uow.DescartarCambios();
        }
        return new ResultadoConfirmacion { Exito = false, Codigo = codigo, Mensaje = mensaje, PedidoId = pedidoId, Rechazo = rechazo };
    }
}

public sealed class ExcepcionValidacionPedido : ExcepcionAplicacion
{
    public RechazoValidacion Rechazo { get; }

    public ExcepcionValidacionPedido(RechazoValidacion rechazo) : base(rechazo.Codigo, $"[{rechazo.Eslabon}] {rechazo.Mensaje}") =>
        Rechazo = rechazo;
}
