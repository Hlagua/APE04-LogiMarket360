namespace LogiMarket.Domain.Eventos;

/// <summary>
/// Evento pendiente (patrón Outbox): se inserta en la MISMA transacción que la operación de negocio y
/// se procesa después de confirmarla. Se marca procesado una sola vez.
/// </summary>
public class EventoOutbox
{
    public int Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public int PedidoId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaProcesado { get; private set; }
    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }

    public bool Procesado => FechaProcesado is not null;

    private EventoOutbox() { }

    public EventoOutbox(string tipo, int pedidoId, string payload, DateTime fecha)
    {
        Tipo = tipo;
        PedidoId = pedidoId;
        Payload = payload;
        FechaCreacion = fecha;
    }

    public void MarcarProcesado(DateTime fecha)
    {
        if (Procesado) return; // idempotente
        Intentos++;
        FechaProcesado = fecha;
        UltimoError = null;
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error.Length > 500 ? error[..500] : error;
    }
}

/// <summary>Auditoría de negocio y de seguridad persistida.</summary>
public class Auditoria
{
    public int Id { get; private set; }
    public DateTime Fecha { get; private set; }
    public string Usuario { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public string Accion { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;
    public int? EventoOrigenId { get; private set; }

    private Auditoria() { }

    public Auditoria(DateTime fecha, string usuario, string categoria, string accion, string detalle, int? eventoOrigenId = null)
    {
        Fecha = fecha;
        Usuario = usuario;
        Categoria = categoria;
        Accion = accion;
        Detalle = detalle.Length > 1000 ? detalle[..1000] : detalle;
        EventoOrigenId = eventoOrigenId;
    }
}
