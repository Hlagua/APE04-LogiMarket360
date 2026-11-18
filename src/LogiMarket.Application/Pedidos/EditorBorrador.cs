using LogiMarket.Application.Comun;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Pedidos;

/// <summary>
/// Sesión de edición del carrito/borrador en memoria. Usa Builder para crear el agregado y el Memento
/// (<see cref="HistorialBorrador"/>) para deshacer/rehacer hasta 5 estados completos. Solo se capturan
/// los cambios que tuvieron éxito.
/// </summary>
public sealed class EditorBorrador
{
    private readonly ServicioValoracion _valoracion;
    private Cliente? _clientePendiente;
    private TipoEntrega _entregaPendiente = TipoEntrega.ESTANDAR;

    public EditorBorrador(ServicioValoracion valoracion, Pedido? existente = null)
    {
        _valoracion = valoracion;
        Pedido = existente;
        if (existente is not null && !existente.PermiteEdicion)
            throw new ExcepcionAplicacion("BORRADOR_NO_EDITABLE", $"El pedido {existente.Numero} está en estado {existente.Estado} y no es editable.");
    }

    public Pedido? Pedido { get; private set; }
    public HistorialBorrador Historial { get; } = new();
    public int PedidoId => Pedido?.Id ?? 0;
    public bool TieneCambiosSinGuardar { get; private set; }
    public string ClienteNombre => Pedido?.ClienteNombre ?? _clientePendiente?.Nombre ?? "(sin cliente)";
    public TipoEntrega Entrega => Pedido?.TipoEntrega ?? _entregaPendiente;

    public void SeleccionarCliente(Cliente cliente)
    {
        if (Pedido is null) { _clientePendiente = cliente; return; }
        Aplicar(p => p.CambiarCliente(cliente));
    }

    public void CambiarEntrega(TipoEntrega entrega)
    {
        if (Pedido is null) { _entregaPendiente = entrega; return; }
        Aplicar(p => p.CambiarEntrega(entrega));
    }

    public void Agregar(IElementoCatalogo elemento, int cantidad)
    {
        if (Pedido is null)
        {
            // Builder: impide crear un agregado sin cliente o sin líneas.
            var builder = new PedidoBuilder().ConEntrega(_entregaPendiente).Agregar(elemento, cantidad);
            if (_clientePendiente is not null) builder.ParaCliente(_clientePendiente);
            Pedido = builder.Construir();
            TieneCambiosSinGuardar = true;
            return;
        }
        Aplicar(p => p.AgregarLinea(elemento, cantidad));
    }

    public void CambiarCantidad(string codigo, int cantidad) => Aplicar(p => p.CambiarCantidad(codigo, cantidad));

    public void Quitar(string codigo) => Aplicar(p => p.QuitarLinea(codigo));

    public bool Deshacer()
    {
        var ok = Pedido is not null && Historial.Deshacer(Pedido);
        if (ok) TieneCambiosSinGuardar = true;
        return ok;
    }

    public bool Rehacer()
    {
        var ok = Pedido is not null && Historial.Rehacer(Pedido);
        if (ok) TieneCambiosSinGuardar = true;
        return ok;
    }

    public ResumenValoracion? Valorar() => Pedido is null ? null : _valoracion.Valorar(Pedido);

    public void MarcarGuardado() => TieneCambiosSinGuardar = false;

    /// <summary>Al confirmar el pedido se bloquea el historial: no se puede deshacer un cobro ni el stock.</summary>
    public void BloquearPorConfirmacion() => Historial.Bloquear();

    private void Aplicar(Action<Pedido> cambio)
    {
        if (Pedido is null) throw new ExcepcionAplicacion("BORRADOR_VACIO", "Agregue primero un producto o kit.");
        var anterior = Pedido.CrearMemento();
        cambio(Pedido);                       // si lanza, no se registra el estado
        Historial.RegistrarAnterior(anterior);
        TieneCambiosSinGuardar = true;
    }
}
