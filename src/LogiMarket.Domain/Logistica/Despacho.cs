using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Logistica;

//despacho con guia simulada
//uno por pedido
public class Despacho
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string NumeroGuia { get; private set; } = string.Empty;
    public string Familia { get; private set; } = string.Empty;
    public int PlazoDias { get; private set; }
    public string Etiqueta { get; private set; } = string.Empty;
    public DateTime FechaDespacho { get; private set; }
    public DateTime FechaEstimadaEntrega { get; private set; }
    public DateTime? FechaEntrega { get; private set; }
    public string Usuario { get; private set; } = string.Empty;

    private Despacho() { }

    public Despacho(int pedidoId, string numeroGuia, string familia, int plazoDias, string etiqueta,
        DateTime fechaDespacho, DateTime fechaEstimada, string usuario)
    {
        PedidoId = pedidoId;
        NumeroGuia = Guardia.Texto(numeroGuia, "guía", 40);
        Familia = familia;
        PlazoDias = plazoDias;
        Etiqueta = etiqueta;
        FechaDespacho = fechaDespacho;
        FechaEstimadaEntrega = fechaEstimada;
        Usuario = usuario;
    }

    public void RegistrarEntrega(DateTime fecha)
    {
        if (FechaEntrega is not null)
            throw new ExcepcionDominio("ENTREGA_DUPLICADA", $"La guía {NumeroGuia} ya registra entrega.");
        FechaEntrega = fecha;
    }

    public string EstadoSeguimiento =>
        FechaEntrega is null ? $"En tránsito ({Familia}), llegada estimada {FechaEstimadaEntrega:yyyy-MM-dd}" : $"Entregado el {FechaEntrega:yyyy-MM-dd HH:mm}";
}
