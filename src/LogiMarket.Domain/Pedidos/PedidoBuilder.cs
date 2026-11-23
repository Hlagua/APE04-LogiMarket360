using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

//patron builder (creacional)
//clase PedidoBuilder
//objetivo: ensambla paso a paso cliente, lineas simples y kits (con su instantanea) y forma de entrega
//construir solo entrega un agregado VALIDO: sin cliente o sin lineas falla
public sealed class PedidoBuilder
{
    private (int Id, string Nombre, string Email, string Telefono, CategoriaCliente Categoria)? _cliente;
    private TipoEntrega _entrega = TipoEntrega.ESTANDAR;
    private readonly List<(IElementoCatalogo Elemento, int Cantidad)> _elementos = new();
    private DateTime _fecha = DateTime.Now;
    private string _usuario = "sistema";

    public PedidoBuilder ParaCliente(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        if (!cliente.Activo)
            throw new ExcepcionDominio("CLIENTE_INACTIVO", $"El cliente {cliente.Nombre} está inactivo.");
        _cliente = (cliente.Id, cliente.Nombre, cliente.Email, cliente.Telefono, cliente.Categoria);
        return this;
    }

    public PedidoBuilder ParaCliente(int id, string nombre, CategoriaCliente categoria, string email = "", string telefono = "")
    {
        _cliente = (id, Guardia.Texto(nombre, "cliente", 120), email, telefono, categoria);
        return this;
    }

    public PedidoBuilder ConEntrega(TipoEntrega entrega)
    {
        _entrega = entrega;
        return this;
    }

    public PedidoBuilder Agregar(IElementoCatalogo elemento, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(elemento);
        _elementos.Add((elemento, Guardia.CantidadPositiva(cantidad)));
        return this;
    }

    public PedidoBuilder EnFecha(DateTime fecha)
    {
        _fecha = fecha;
        return this;
    }

    public PedidoBuilder CreadoPor(string usuario)
    {
        _usuario = string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario;
        return this;
    }

    public Pedido Construir()
    {
        if (_cliente is null)
            throw new ExcepcionDominio("PEDIDO_SIN_CLIENTE", "No se puede construir un pedido sin cliente.");
        if (_elementos.Count == 0)
            throw new ExcepcionDominio("PEDIDO_SIN_LINEAS", "No se puede construir un pedido sin líneas.");

        //se agrupan elementos repetidos para que cada codigo sea una sola linea
        var lineas = _elementos
            .GroupBy(e => e.Elemento.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(g => PedidoDetalle.DesdeElemento(g.First().Elemento, g.Sum(x => x.Cantidad)))
            .ToList();

        var c = _cliente.Value;
        return new Pedido(c.Id, c.Nombre, c.Email, c.Telefono, c.Categoria, _entrega, lineas, _fecha, _usuario);
    }
}
