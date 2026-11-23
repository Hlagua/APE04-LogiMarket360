using LogiMarket.Application.Comun;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Pedidos;

//sesion de edicion del carrito/borrador en memoria
//usa Builder para crear el agregado
//usa el Memento (HistorialBorrador) para deshacer/rehacer hasta 5 estados completos
//INCLUIDO el carrito vacio: la primera linea y el vaciado tambien se pueden deshacer
//solo se registran cambios exitosos
public sealed class EditorBorrador
{
    private sealed record ClienteBorrador(int Id, string Nombre, string Email, string Telefono, CategoriaCliente Categoria);

    private readonly IServicioValoracion _valoracion;
    private ClienteBorrador? _cliente;
    private TipoEntrega _entrega = TipoEntrega.ESTANDAR;

    //agregado retenido cuando el carrito queda vacio
    //si se deshace el vaciado se reutiliza el mismo objeto
    //de modo que un borrador ya guardado conserva su identificador
    private Pedido? _retenido;

    public EditorBorrador(IServicioValoracion valoracion, Pedido? existente = null)
    {
        _valoracion = valoracion;
        if (existente is not null && !existente.PermiteEdicion)
            throw new ExcepcionAplicacion("BORRADOR_NO_EDITABLE", $"El pedido {existente.Numero} está en estado {existente.Estado} y no es editable.");
        Pedido = existente;
        if (existente is not null) RecordarDatos(existente);
    }

    public Pedido? Pedido { get; private set; }
    public HistorialBorrador Historial { get; } = new();
    public int PedidoId => Pedido?.Id ?? _retenido?.Id ?? 0;
    public bool TieneCambiosSinGuardar { get; private set; }
    public bool EstaVacio => Pedido is null;
    public string ClienteNombre => _cliente?.Nombre ?? "(sin cliente)";
    public TipoEntrega Entrega => Pedido?.TipoEntrega ?? _entrega;

    public void SeleccionarCliente(Cliente cliente)
    {
        if (Pedido is null) { _cliente = Datos(cliente); return; }
        Aplicar(p => p.CambiarCliente(cliente));
    }

    //selecciona el cliente a partir del DTO del lado de lectura
    public void SeleccionarCliente(Consultas.ClienteDto cliente)
    {
        if (!cliente.Activo)
            throw new ExcepcionAplicacion("CLIENTE_INACTIVO", $"El cliente {cliente.Nombre} está inactivo.");
        var categoria = Enum.Parse<CategoriaCliente>(cliente.Categoria);
        if (Pedido is null) { _cliente = new ClienteBorrador(cliente.Id, cliente.Nombre, cliente.Email, cliente.Telefono, categoria); return; }
        Aplicar(p => p.CambiarCliente(cliente.Id, cliente.Nombre, cliente.Email, cliente.Telefono, categoria));
    }

    public void CambiarEntrega(TipoEntrega entrega)
    {
        if (Pedido is null) { _entrega = entrega; return; }
        Aplicar(p => p.CambiarEntrega(entrega));
    }

    public void Agregar(IElementoCatalogo elemento, int cantidad)
    {
        if (Pedido is not null)
        {
            Aplicar(p => p.AgregarLinea(elemento, cantidad));
            return;
        }

        if (_retenido is not null)
        {
            //carrito vaciado de un borrador existente: se reutiliza el agregado (conserva su id)
            var vacio = _retenido;
            CambiarAEstado(null, () =>
            {
                if (_cliente is not null)
                    vacio.CambiarCliente(_cliente.Id, _cliente.Nombre, _cliente.Email, _cliente.Telefono, _cliente.Categoria);
                vacio.CambiarEntrega(_entrega);
                ReemplazarLineas(vacio, elemento, cantidad);
                Pedido = vacio;
                _retenido = null;
            });
            return;
        }

        //builder: impide crear un agregado sin cliente o sin lineas
        var builder = new PedidoBuilder().ConEntrega(_entrega).Agregar(elemento, cantidad);
        if (_cliente is not null)
            builder.ParaCliente(_cliente.Id, _cliente.Nombre, _cliente.Categoria, _cliente.Email, _cliente.Telefono);
        var nuevo = builder.Construir();
        Historial.RegistrarAnterior(null); //el carrito vacio es un estado al que se puede volver
        Pedido = nuevo;
        RecordarDatos(nuevo);
        TieneCambiosSinGuardar = true;
    }

    public void CambiarCantidad(string codigo, int cantidad) => Aplicar(p => p.CambiarCantidad(codigo, cantidad));

    //quita una linea
    //si es la ultima, el carrito queda vacio (y se puede deshacer)
    public void Quitar(string codigo)
    {
        if (Pedido is null) throw new ExcepcionAplicacion("BORRADOR_VACIO", "El carrito ya está vacío.");
        if (Pedido.Lineas.Count > 1)
        {
            Aplicar(p => p.QuitarLinea(codigo));
            return;
        }
        if (!Pedido.Lineas.Any(l => string.Equals(l.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
            throw new ExcepcionAplicacion("LINEA_NO_EXISTE", $"El carrito no contiene la línea {codigo}.");
        Vaciar();
    }

    //vacia el carrito conservando cliente y forma de entrega
    //se puede deshacer
    public void Vaciar()
    {
        if (Pedido is null) return;
        Historial.RegistrarAnterior(Pedido.CrearMemento());
        RecordarDatos(Pedido);
        _retenido = Pedido;
        Pedido = null;
        TieneCambiosSinGuardar = true;
    }

    public bool Deshacer()
    {
        if (!Historial.Deshacer(Pedido?.CrearMemento(), out var anterior)) return false;
        AplicarEstado(anterior);
        return true;
    }

    public bool Rehacer()
    {
        if (!Historial.Rehacer(Pedido?.CrearMemento(), out var siguiente)) return false;
        AplicarEstado(siguiente);
        return true;
    }

    public ResumenValoracion? Valorar() => Pedido is null ? null : _valoracion.Valorar(Pedido);

    public void MarcarGuardado() => TieneCambiosSinGuardar = false;

    //al confirmar el pedido se bloquea el historial: no se puede deshacer un cobro ni el stock
    public void BloquearPorConfirmacion() => Historial.Bloquear();

    //internos

    private void Aplicar(Action<Pedido> cambio)
    {
        if (Pedido is null) throw new ExcepcionAplicacion("BORRADOR_VACIO", "Agregue primero un producto o kit.");
        var anterior = Pedido.CrearMemento();
        cambio(Pedido); //si lanza, no se registra el estado
        Historial.RegistrarAnterior(anterior);
        RecordarDatos(Pedido);
        TieneCambiosSinGuardar = true;
    }

    private void CambiarAEstado(PedidoMemento? anterior, Action cambio)
    {
        cambio();
        Historial.RegistrarAnterior(anterior);
        TieneCambiosSinGuardar = true;
    }

    //restaura un estado del historial: null = carrito vacio
    private void AplicarEstado(PedidoMemento? estado)
    {
        if (estado is null)
        {
            if (Pedido is not null)
            {
                RecordarDatos(Pedido);
                _retenido = Pedido;
                Pedido = null;
            }
        }
        else if (Pedido is not null)
        {
            Pedido.Restaurar(estado);
        }
        else if (_retenido is not null)
        {
            _retenido.Restaurar(estado);
            Pedido = _retenido;
            _retenido = null;
        }
        else
        {
            Pedido = Pedido.DesdeMemento(estado, DateTime.Now, "sistema");
        }
        if (Pedido is not null) RecordarDatos(Pedido);
        TieneCambiosSinGuardar = true;
    }

    private static void ReemplazarLineas(Pedido pedido, IElementoCatalogo elemento, int cantidad)
    {
        //se agrega la nueva linea y luego se quitan las anteriores (nunca queda sin lineas)
        var anteriores = pedido.Lineas.Select(l => l.Codigo).Where(c => !string.Equals(c, elemento.Codigo, StringComparison.OrdinalIgnoreCase)).ToList();
        var existia = pedido.Lineas.Any(l => string.Equals(l.Codigo, elemento.Codigo, StringComparison.OrdinalIgnoreCase));
        if (existia) pedido.CambiarCantidad(elemento.Codigo, cantidad);
        else pedido.AgregarLinea(elemento, cantidad);
        foreach (var codigo in anteriores) pedido.QuitarLinea(codigo);
    }

    private void RecordarDatos(Pedido p)
    {
        if (p.ClienteId != 0)
            _cliente = new ClienteBorrador(p.ClienteId, p.ClienteNombre, p.ClienteEmail, p.ClienteTelefono, p.CategoriaCliente);
        _entrega = p.TipoEntrega;
    }

    private static ClienteBorrador Datos(Cliente c) => new(c.Id, c.Nombre, c.Email, c.Telefono, c.Categoria);
}
