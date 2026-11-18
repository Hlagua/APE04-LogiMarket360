using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

/// <summary>Cobro simulado APROBADO. Referencia y clave de idempotencia son únicas (índices en BD).</summary>
public class Pago
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Referencia { get; private set; } = string.Empty;
    public string ClaveIdempotencia { get; private set; } = string.Empty;
    public decimal Monto { get; private set; }
    public string Proveedor { get; private set; } = string.Empty;
    public string CorrelacionId { get; private set; } = string.Empty;
    public DateTime Fecha { get; private set; }

    private Pago() { }

    public static Pago Aprobado(int pedidoId, string referencia, string claveIdempotencia, decimal monto,
        string proveedor, string correlacionId, DateTime fecha)
    {
        return new Pago
        {
            PedidoId = pedidoId,
            Referencia = Guardia.Texto(referencia, "referencia", 100),
            ClaveIdempotencia = Guardia.Texto(claveIdempotencia, "clave", 100),
            Monto = Guardia.PrecioPositivo(monto),
            Proveedor = proveedor,
            CorrelacionId = correlacionId,
            Fecha = fecha
        };
    }
}
