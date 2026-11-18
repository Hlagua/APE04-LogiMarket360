using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Devoluciones;

public enum MotivoDevolucion
{
    DEFECTO,
    ARREPENTIMIENTO
}

/// <summary>Devolución TOTAL aceptada (única por pedido) con su reembolso simulado único.</summary>
public class Devolucion
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public MotivoDevolucion Motivo { get; private set; }
    public DateTime FechaSolicitud { get; private set; }
    public int DiasDesdeEntrega { get; private set; }
    public decimal MontoReembolso { get; private set; }
    public string ReferenciaReembolso { get; private set; } = string.Empty;
    public bool StockReintegrado { get; private set; }
    public string Observacion { get; private set; } = string.Empty;
    public string Usuario { get; private set; } = string.Empty;

    private Devolucion() { }

    public Devolucion(int pedidoId, MotivoDevolucion motivo, DateTime fechaSolicitud, int diasDesdeEntrega,
        decimal montoReembolso, string referenciaReembolso, bool stockReintegrado, string observacion, string usuario)
    {
        PedidoId = pedidoId;
        Motivo = motivo;
        FechaSolicitud = fechaSolicitud;
        DiasDesdeEntrega = diasDesdeEntrega;
        MontoReembolso = Dinero.Redondear(Guardia.NoNegativo(montoReembolso, "reembolso"));
        ReferenciaReembolso = Guardia.Texto(referenciaReembolso, "referencia de reembolso", 100);
        StockReintegrado = stockReintegrado;
        Observacion = observacion ?? string.Empty;
        Usuario = usuario;
    }
}
