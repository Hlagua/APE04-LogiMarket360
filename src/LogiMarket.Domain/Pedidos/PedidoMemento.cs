using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

//patron memento (comportamental)
//clase PedidoMemento
//objetivo: instantanea COMPLETA e inmutable del borrador (cliente, entrega, lineas y sus componentes anidados)
//solo el agregado Pedido conoce su contenido interno
public sealed class PedidoMemento
{
    internal int ClienteId { get; }
    internal string ClienteNombre { get; }
    internal string ClienteEmail { get; }
    internal string ClienteTelefono { get; }
    internal CategoriaCliente Categoria { get; }
    internal TipoEntrega TipoEntrega { get; }
    internal IReadOnlyList<PedidoDetalle> Lineas { get; }

    //descripcion legible (no expone el estado interno modificable)
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

//caretaker del Memento: hasta 5 estados para deshacer y su pila de rehacer
//un estado puede ser null = carrito vacio (antes de la primera linea o tras quitar la ultima)
//de modo que deshacer tambien regresa al carrito vacio
//al confirmar el pedido el historial se bloquea
//limpia (no se puede deshacer un cobro ni un descuento de stock)
public sealed class HistorialBorrador
{
    public const int Capacidad = ParametrosNegocio.CapacidadHistorialBorrador;
    private readonly LinkedList<PedidoMemento?> _deshacer = new();
    private readonly Stack<PedidoMemento?> _rehacer = new();

    public bool Bloqueado { get; private set; }
    public int CantidadDeshacer => _deshacer.Count;
    public int CantidadRehacer => _rehacer.Count;
    public bool PuedeDeshacer => !Bloqueado && _deshacer.Count > 0;
    public bool PuedeRehacer => !Bloqueado && _rehacer.Count > 0;

    //guarda el estado actual ANTES de un cambio
    public void Capturar(Pedido pedido) => RegistrarAnterior(pedido.CrearMemento());

    //registra un estado capturado previamente (null = carrito vacio)
    public void RegistrarAnterior(PedidoMemento? memento)
    {
        ExigirDesbloqueado();
        _deshacer.AddLast(memento);
        if (_deshacer.Count > Capacidad) _deshacer.RemoveFirst();
        _rehacer.Clear();
    }

    //entrega el estado anterior y guarda el actual para rehacer
    public bool Deshacer(PedidoMemento? actual, out PedidoMemento? anterior)
    {
        anterior = null;
        if (!PuedeDeshacer) return false;
        anterior = _deshacer.Last!.Value;
        _deshacer.RemoveLast();
        _rehacer.Push(actual);
        return true;
    }

    //entrega el estado siguiente y guarda el actual para deshacer
    public bool Rehacer(PedidoMemento? actual, out PedidoMemento? siguiente)
    {
        siguiente = null;
        if (!PuedeRehacer) return false;
        _deshacer.AddLast(actual);
        if (_deshacer.Count > Capacidad) _deshacer.RemoveFirst();
        siguiente = _rehacer.Pop();
        return true;
    }

    //atajo cuando el pedido nunca pasa por el carrito vacio
    public bool Deshacer(Pedido pedido) => Aplicar(pedido, Deshacer(pedido.CrearMemento(), out var m), m);

    //atajo cuando el pedido nunca pasa por el carrito vacio
    public bool Rehacer(Pedido pedido) => Aplicar(pedido, Rehacer(pedido.CrearMemento(), out var m), m);

    private static bool Aplicar(Pedido pedido, bool hubo, PedidoMemento? memento)
    {
        if (!hubo) return false;
        if (memento is null)
            throw new InvalidOperationException("El estado guardado es el carrito vacío: use EditorBorrador para restaurarlo.");
        pedido.Restaurar(memento);
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
