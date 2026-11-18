namespace LogiMarket.Application.Puertos;

public sealed record ResultadoCobro(bool Aprobado, string Referencia, string Proveedor, string Mensaje)
{
    public string CorrelacionId { get; init; } = "-";
}

/// <summary>
/// Puerto de cobro simulado. Cadena en ejecución: PagoAutorizadoProxy → AuditoriaPagoDecorator →
/// (PasarelaLegadaAdapter | PasarelaModernaSimulada).
/// </summary>
public interface IPasarelaPago
{
    string Nombre { get; }
    Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default);
}

/// <summary>Reembolso simulado separado del cobro (ISP: un proveedor de cobro no está obligado a reembolsar).</summary>
public interface IServicioReembolso
{
    Task<string> ReembolsarAsync(int pedidoId, decimal monto, string referenciaPago, CancellationToken ct = default);
}

/// <summary>Producto del Factory Method de avisos.</summary>
public interface INotificador
{
    string Canal { get; }
    Task EnviarAsync(string destinatario, string asunto, string mensaje, CancellationToken ct = default);
}

public sealed record ResultadoSalida(bool Exito, string Medio, string Destino, int Caracteres);

/// <summary>Implementador del patrón Bridge: medio por el que sale un comprobante.</summary>
public interface ISalidaDocumento
{
    string Nombre { get; }
    Task<ResultadoSalida> EmitirAsync(string titulo, string contenido, CancellationToken ct = default);
}
