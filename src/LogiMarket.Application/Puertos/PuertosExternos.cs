namespace LogiMarket.Application.Puertos;

public sealed record ResultadoCobro(bool Aprobado, string Referencia, string Proveedor, string Mensaje)
{
    public string CorrelacionId { get; init; } = "-";
}

//puerto de cobro simulado
//cadena en ejecucion:
//PagoAutorizadoProxy -> AuditoriaPagoDecorator -> (PasarelaLegadaAdapter | PasarelaModernaSimulada)
public interface IPasarelaPago
{
    string Nombre { get; }
    Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default);
}

//reembolso simulado separado del cobro (ISP: un proveedor de cobro no esta obligado a reembolsar)
public interface IServicioReembolso
{
    Task<string> ReembolsarAsync(int pedidoId, decimal monto, string referenciaPago, CancellationToken ct = default);
}

//producto del Factory Method de avisos
public interface INotificador
{
    string Canal { get; }
    Task EnviarAsync(string destinatario, string asunto, string mensaje, CancellationToken ct = default);
}

public sealed record ResultadoSalida(bool Exito, string Medio, string Destino, int Caracteres);

//implementador del patron Bridge: medio por el que sale un comprobante
public interface ISalidaDocumento
{
    string Nombre { get; }
    Task<ResultadoSalida> EmitirAsync(string titulo, string contenido, CancellationToken ct = default);
}
