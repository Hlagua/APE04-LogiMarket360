namespace LogiMarket.WinForms.Infraestructura;

/// <summary>Sucesos de la interfaz que el mediador sabe coordinar.</summary>
public enum EventoVista
{
    SesionIniciada,
    CatalogoCambiado,
    ClientesCambiados,
    BorradorGuardado,
    PedidoConfirmado,
    PedidoCambiado,
    InventarioCambiado,
    DevolucionRegistrada,
    MetricasCambiadas,
    AbrirPedidoEnCobro,
    AbrirPedidoEnDespacho,
    AbrirPedidoEnDevolucion,
    AbrirBorradorEnCarrito,
    BaseRestablecida
}

/// <summary>Colega del Mediator: una vista que puede refrescarse o recibir un pedido seleccionado.</summary>
public interface IVistaColega
{
    string Clave { get; }
    Task RefrescarAsync();
    Task RecibirPedidoAsync(int pedidoId);
}

public interface IMediadorVistas
{
    IReadOnlyList<string> Trazas { get; }
    void Registrar(IVistaColega vista);
    Task NotificarAsync(IVistaColega? emisor, EventoVista evento, int pedidoId = 0);
}

/// <summary>
/// Patrón Mediator: FormularioPrincipalMediator coordina catálogo, carrito, pedidos, inventario y
/// métricas. Ninguna vista llama métodos de otra; solo notifican sucesos aquí.
/// </summary>
public sealed class FormularioPrincipalMediator : IMediadorVistas
{
    public const string Panel = "panel", Catalogo = "catalogo", Clientes = "clientes", Carrito = "carrito",
        Pedidos = "pedidos", Cobro = "cobro", Despacho = "despacho", Devoluciones = "devoluciones",
        Inventario = "inventario", Reportes = "reportes", Auditoria = "auditoria", Demo = "demo";

    private readonly Dictionary<string, IVistaColega> _vistas = new();
    private readonly List<string> _trazas = new();

    /// <summary>Lo asigna el formulario principal para poder cambiar de pestaña.</summary>
    public Action<string>? MostrarVista { get; set; }

    public IReadOnlyList<string> Trazas => _trazas;

    public void Registrar(IVistaColega vista) => _vistas[vista.Clave] = vista;

    public async Task NotificarAsync(IVistaColega? emisor, EventoVista evento, int pedidoId = 0)
    {
        _trazas.Add($"{DateTime.Now:HH:mm:ss} {emisor?.Clave ?? "sistema"} → {evento} {(pedidoId > 0 ? $"#{pedidoId}" : "")}");
        switch (evento)
        {
            case EventoVista.PedidoConfirmado:
            case EventoVista.DevolucionRegistrada:
                await RefrescarAsync(emisor, Catalogo, Pedidos, Inventario, Panel, Auditoria, Cobro, Despacho, Devoluciones);
                break;
            case EventoVista.PedidoCambiado:
            case EventoVista.BorradorGuardado:
                await RefrescarAsync(emisor, Pedidos, Panel, Cobro, Despacho, Devoluciones, Auditoria);
                break;
            case EventoVista.CatalogoCambiado:
            case EventoVista.InventarioCambiado:
                await RefrescarAsync(emisor, Catalogo, Carrito, Inventario, Panel, Reportes);
                break;
            case EventoVista.ClientesCambiados:
                await RefrescarAsync(emisor, Clientes, Carrito);
                break;
            case EventoVista.MetricasCambiadas:
                await RefrescarAsync(emisor, Auditoria, Panel);
                break;
            case EventoVista.AbrirPedidoEnCobro:
                await EntregarAsync(Cobro, pedidoId);
                break;
            case EventoVista.AbrirPedidoEnDespacho:
                await EntregarAsync(Despacho, pedidoId);
                break;
            case EventoVista.AbrirPedidoEnDevolucion:
                await EntregarAsync(Devoluciones, pedidoId);
                break;
            case EventoVista.AbrirBorradorEnCarrito:
                await EntregarAsync(Carrito, pedidoId);
                break;
            case EventoVista.SesionIniciada:
            case EventoVista.BaseRestablecida:
                await RefrescarAsync(null, _vistas.Keys.ToArray());
                break;
        }
    }

    private async Task RefrescarAsync(IVistaColega? emisor, params string[] claves)
    {
        foreach (var clave in claves)
            if (_vistas.TryGetValue(clave, out var v) && !ReferenceEquals(v, emisor))
                await v.RefrescarAsync();
    }

    private async Task EntregarAsync(string clave, int pedidoId)
    {
        if (!_vistas.TryGetValue(clave, out var vista)) return;
        MostrarVista?.Invoke(clave);
        await vista.RecibirPedidoAsync(pedidoId);
    }
}
