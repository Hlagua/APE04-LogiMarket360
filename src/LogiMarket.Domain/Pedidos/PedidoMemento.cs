using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

/// <summary>
/// Patrón Memento: instantánea COMPLETA e inmutable del borrador (cliente, entrega, líneas y sus
/// componentes anidados). Solo el agregado Pedido conoce su contenido interno.
/// </summary>
public sealed class PedidoMemento
{
    internal int ClienteId { get; }
    internal string ClienteNombre { get; }
    internal string ClienteEmail { get; }
    internal string ClienteTelefono { get; }
    internal CategoriaCliente Categoria { get; }
    internal TipoEntrega TipoEntrega { get; }
    internal IReadOnlyList<PedidoDetalle> Lineas { get; }

    /// <summary>Descripción legible (no expone el estado interno modificable).</summary>
    public string Descripcion { get; }

    internal PedidoMemento(int clienteId, string clienteNombre, string email, string telefono,
        CategoriaCliente categoria, TipoEntrega entrega, IReadOnlyList<PedidoDetalle> lineas)
    {
        ClienteId = clienteId;
        ClienteNombre = clienteNombre;
        ClienteEmail = email;
        ClienteTelefono = telefono;
        Categoria = categoria;
        TipoEntrega = entrega;
        Lineas = lineas;
        Descripcion = $"{clienteNombre} | {entrega} | " +
                      string.Join(", ", lineas.Select(l => $"{l.Codigo}x{l.Cantidad}"));
    }
}

/// <summary>
/// Caretaker del Memento: hasta 5 estados para deshacer y su pila de rehacer. Al confirmar el pedido
/// el historial se bloquea y limpia (no se puede deshacer un cobro ni un descuento de stock).
/// </summary>
public sealed class HistorialBorrador
{
    public const int Capacidad = 5;
    private readonly LinkedList<PedidoMemento> _deshacer = new();
    private readonly Stack<PedidoMemento> _rehacer = new();

    public bool Bloqueado { get; private set; }
    public int CantidadDeshacer => _deshacer.Count;
    public int CantidadRehacer => _rehacer.Count;
    public bool PuedeDeshacer => !Bloqueado && _deshacer.Count > 0;
    public bool PuedeRehacer => !Bloqueado && _rehacer.Count > 0;

    /// <summary>Guarda el estado actual ANTES de un cambio.</summary>
    public void Capturar(Pedido pedido) => RegistrarAnterior(pedido.CrearMemento());

    /// <summary>Registra un estado capturado previamente (útil para capturar solo si el cambio tuvo éxito).</summary>
    public void RegistrarAnterior(PedidoMemento memento)
    {
        ExigirDesbloqueado();
        _deshacer.AddLast(memento);
        if (_deshacer.Count > Capacidad) _deshacer.RemoveFirst();
        _rehacer.Clear();
    }

    public bool Deshacer(Pedido pedido)
    {
        if (!PuedeDeshacer) return false;
        var anterior = _deshacer.Last!.Value;
        _deshacer.RemoveLast();
        _rehacer.Push(pedido.CrearMemento());
        pedido.Restaurar(anterior);
        return true;
    }

    public bool Rehacer(Pedido pedido)
    {
        if (!PuedeRehacer) return false;
        _deshacer.AddLast(pedido.CrearMemento());
        if (_deshacer.Count > Capacidad) _deshacer.RemoveFirst();
        pedido.Restaurar(_rehacer.Pop());
        return true;
    }

    public void Bloquear()
    {
        Bloqueado = true;
        _deshacer.Clear();
        _rehacer.Clear();
    }

    private void ExigirDesbloqueado()
    {
        if (Bloqueado)
            throw new ExcepcionDominio("HISTORIAL_BLOQUEADO", "El pedido ya fue confirmado: su historial de edición está bloqueado.");
    }
}
