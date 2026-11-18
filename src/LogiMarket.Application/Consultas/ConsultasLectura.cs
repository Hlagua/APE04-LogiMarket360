namespace LogiMarket.Application.Consultas;

// Lado de CONSULTA (CQRS básico): DTO de solo lectura obtenidos sin rastreo de cambios.

public sealed record ProductoDto(int Id, string Sku, string Nombre, string Categoria, string Descripcion,
    decimal Precio, decimal PesoKg, int Stock, bool Activo);

public sealed record ClienteDto(int Id, string Identificacion, string Nombre, string Email, string Telefono,
    string Categoria, bool Activo);

public sealed record PedidoResumenDto(int Id, string Numero, int ClienteId, string Cliente, string Categoria,
    string Estado, string Entrega, decimal Subtotal, decimal Descuento, string Politica, decimal Tarifa,
    decimal Total, DateTime FechaCreacion, DateTime? FechaEntrega, bool Devuelto, int Unidades);

public sealed record LineaPedidoDto(string Codigo, string Descripcion, bool EsKit, int Cantidad,
    decimal PrecioUnitario, decimal Subtotal, string Composicion);

public sealed record HistorialEstadoDto(DateTime Fecha, string? Anterior, string Nuevo, string Usuario, string Detalle);

public sealed record PagoDto(int PedidoId, string Referencia, string ClaveIdempotencia, decimal Monto,
    string Proveedor, string CorrelacionId, DateTime Fecha);

public sealed record DespachoDto(int PedidoId, string NumeroGuia, string Familia, int PlazoDias, string Etiqueta,
    DateTime FechaDespacho, DateTime FechaEstimada, DateTime? FechaEntrega, string Seguimiento);

public sealed record DevolucionDto(int PedidoId, string Motivo, DateTime FechaSolicitud, int Dias,
    decimal MontoReembolso, string ReferenciaReembolso, bool StockReintegrado, string Observacion);

public sealed record PedidoCompletoDto(PedidoResumenDto Resumen, string ClienteEmail, IReadOnlyList<LineaPedidoDto> Lineas,
    IReadOnlyList<HistorialEstadoDto> Historial, PagoDto? Pago, DespachoDto? Despacho, DevolucionDto? Devolucion);

public sealed record MovimientoDto(DateTime Fecha, string Sku, int Cantidad, int StockResultante, string Motivo,
    string Referencia, string Usuario);

public sealed record AuditoriaDto(DateTime Fecha, string Usuario, string Categoria, string Accion, string Detalle);

public sealed record OutboxDto(int Id, string Tipo, int PedidoId, DateTime FechaCreacion, DateTime? FechaProcesado,
    int Intentos, string? UltimoError);

public sealed record IndicadoresDto(int Borradores, int Validados, int Pagados, int Despachados, int Entregados,
    int Cancelados, int Devueltos, decimal VentasCobradas, decimal Reembolsado, int ProductosStockBajo,
    int OutboxPendientes, IReadOnlyList<string> Alertas);

/// <summary>Puerto de consultas (implementado con EF Core AsNoTracking en Infrastructure).</summary>
public interface IConsultasLectura
{
    Task<IReadOnlyList<ProductoDto>> ProductosAsync(string? texto, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteDto>> ClientesAsync(string? texto, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoResumenDto>> PedidosAsync(CancellationToken ct = default);
    Task<PedidoCompletoDto?> PedidoAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, CancellationToken ct = default);
    Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(string? categoria, string? texto, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PagoDto>> PagosAsync(CancellationToken ct = default);
    Task<IndicadoresDto> IndicadoresAsync(int umbralStockBajo, CancellationToken ct = default);
}
