using System.Text;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Application.Reportes;

/// <summary>
/// Patrón Bridge (abstracción). El TIPO de comprobante (venta/devolución) varía independientemente del
/// MEDIO de salida (<see cref="ISalidaDocumento"/>: vista previa / archivo TXT): 2 × 2 combinaciones sin
/// crear cuatro clases.
/// </summary>
public abstract class Comprobante
{
    protected readonly ISalidaDocumento Salida;

    protected Comprobante(ISalidaDocumento salida) => Salida = salida;

    public abstract string Titulo { get; }

    protected abstract void EscribirCuerpo(StringBuilder sb);

    public string GenerarContenido()
    {
        // Importes siempre con punto decimal y dos decimales, independientemente de la cultura del equipo.
        var cultura = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            return Construir();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = cultura;
        }
    }

    private string Construir()
    {
        var sb = new StringBuilder();
        sb.AppendLine("LOGIMARKET 360 - COMPROBANTE INTERNO DE DEMOSTRACIÓN");
        sb.AppendLine("(No constituye factura tributaria)");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine(Titulo);
        sb.AppendLine(new string('-', 60));
        EscribirCuerpo(sb);
        sb.AppendLine(new string('=', 60));
        return sb.ToString();
    }

    public Task<ResultadoSalida> EmitirAsync(CancellationToken ct = default) => Salida.EmitirAsync(Titulo, GenerarContenido(), ct);
}

public sealed class ComprobanteVenta : Comprobante
{
    private readonly PedidoCompletoDto _pedido;

    public ComprobanteVenta(PedidoCompletoDto pedido, ISalidaDocumento salida) : base(salida) => _pedido = pedido;

    public override string Titulo => $"COMPROBANTE DE VENTA {_pedido.Resumen.Numero}";

    protected override void EscribirCuerpo(StringBuilder sb)
    {
        var r = _pedido.Resumen;
        sb.AppendLine($"Cliente: {r.Cliente} ({r.Categoria})    Estado: {r.Estado}");
        sb.AppendLine($"Fecha: {r.FechaCreacion:yyyy-MM-dd HH:mm}    Entrega: {r.Entrega}");
        sb.AppendLine();
        sb.AppendLine($"{"Código",-16}{"Cant",6}{"P.Unit",10}{"Subtotal",11}");
        foreach (var l in _pedido.Lineas)
        {
            sb.AppendLine($"{l.Codigo,-16}{l.Cantidad,6}{l.PrecioUnitario,10:0.00}{l.Subtotal,11:0.00}");
            if (l.EsKit) sb.AppendLine($"   Composición: {l.Composicion}");
        }
        sb.AppendLine();
        sb.AppendLine($"Subtotal:            {r.Subtotal,10:0.00}");
        sb.AppendLine($"Descuento ({r.Politica}): {r.Descuento,10:0.00}");
        sb.AppendLine($"Envío ({r.Entrega}):   {r.Tarifa,10:0.00}");
        sb.AppendLine($"TOTAL USD:           {r.Total,10:0.00}");
        if (_pedido.Pago is not null)
            sb.AppendLine($"Pago: ref {_pedido.Pago.Referencia} ({_pedido.Pago.Proveedor}) {_pedido.Pago.Fecha:yyyy-MM-dd HH:mm}");
    }
}

public sealed class ComprobanteDevolucion : Comprobante
{
    private readonly PedidoCompletoDto _pedido;

    public ComprobanteDevolucion(PedidoCompletoDto pedido, ISalidaDocumento salida) : base(salida)
    {
        if (pedido.Devolucion is null) throw new InvalidOperationException("El pedido no tiene devolución registrada.");
        _pedido = pedido;
    }

    public override string Titulo => $"COMPROBANTE DE DEVOLUCIÓN {_pedido.Resumen.Numero}";

    protected override void EscribirCuerpo(StringBuilder sb)
    {
        var d = _pedido.Devolucion!;
        var r = _pedido.Resumen;
        sb.AppendLine($"Cliente: {r.Cliente}");
        sb.AppendLine($"Motivo: {d.Motivo}    Días desde la entrega: {d.Dias}");
        sb.AppendLine($"Fecha de solicitud: {d.FechaSolicitud:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Total pagado: {r.Total:0.00}  (tarifa de envío {r.Tarifa:0.00} NO reembolsable)");
        sb.AppendLine($"REEMBOLSO USD: {d.MontoReembolso:0.00}    Ref: {d.ReferenciaReembolso}");
        sb.AppendLine($"Stock reintegrado: {(d.StockReintegrado ? "Sí" : "No")}");
        foreach (var l in _pedido.Lineas) sb.AppendLine($"  {l.Codigo} x{l.Cantidad}");
        if (!string.IsNullOrWhiteSpace(d.Observacion)) sb.AppendLine($"Observación: {d.Observacion}");
    }
}
