namespace LogiMarket.Domain.Inventario;

public enum MotivoMovimiento
{
    INICIAL,
    VENTA,
    DEVOLUCION,
    AJUSTE
}

/// <summary>Kardex: movimiento positivo (entrada) o negativo (salida) con motivo y referencia.</summary>
public class MovimientoInventario
{
    public int Id { get; private set; }
    public int ProductoId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Cantidad { get; private set; }
    public int StockResultante { get; private set; }
    public MotivoMovimiento Motivo { get; private set; }
    public string Referencia { get; private set; } = string.Empty;
    public DateTime Fecha { get; private set; }
    public string Usuario { get; private set; } = string.Empty;

    private MovimientoInventario() { }

    public MovimientoInventario(int productoId, string sku, int cantidad, int stockResultante,
        MotivoMovimiento motivo, string referencia, DateTime fecha, string usuario)
    {
        if (cantidad == 0) throw new ArgumentException("Un movimiento no puede ser cero.", nameof(cantidad));
        ProductoId = productoId;
        Sku = sku;
        Cantidad = cantidad;
        StockResultante = stockResultante;
        Motivo = motivo;
        Referencia = referencia;
        Fecha = fecha;
        Usuario = usuario;
    }
}
