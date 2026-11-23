using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Domain.Pedidos;

//agregado raiz Pedido
//sin setters publicos: toda modificacion pasa por metodos que consultan el objeto de estado (State)
//se construye con PedidoBuilder
//se clona como plantilla (Prototype) y captura/restaura su contenido con PedidoMemento (Memento)
public class Pedido : IPrototipo<Pedido>
{
    private readonly List<PedidoDetalle> _lineas = new();
    private readonly List<PedidoHistorialEstado> _historial = new();

    public int Id { get; private set; }
    public int ClienteId { get; private set; }
    public string ClienteNombre { get; private set; } = string.Empty;
    public string ClienteEmail { get; private set; } = string.Empty;
    public string ClienteTelefono { get; private set; } = string.Empty;
    public CategoriaCliente CategoriaCliente { get; private set; }
    public TipoEntrega TipoEntrega { get; private set; }
    public EstadoPedido Estado { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Descuento { get; private set; }
    public string PoliticaDescuento { get; private set; } = "NORMAL";
    public decimal TarifaEnvio { get; private set; }
    public decimal Total { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaPago { get; private set; }
    public DateTime? FechaEntrega { get; private set; }
    public string? ClaveIdempotencia { get; private set; }
    public bool Devuelto { get; private set; }
    public string CreadoPor { get; private set; } = string.Empty;
    public int Version { get; private set; }

    public IReadOnlyCollection<PedidoDetalle> Lineas => _lineas.AsReadOnly();
    public IReadOnlyCollection<PedidoHistorialEstado> Historial => _historial.AsReadOnly();

    public string Numero => Id == 0 ? "(nuevo)" : $"PED-{Id:000000}";
    public int UnidadesFisicas => _lineas.Sum(l => l.UnidadesFisicas);
    public decimal PesoKg => _lineas.Sum(l => l.PesoKg);
    public decimal SubtotalCalculado => Dinero.Redondear(_lineas.Sum(l => l.Subtotal));

    //importe de productos despues del descuento (sin tarifa de envio): base del reembolso
    public decimal ImporteReembolsable => Dinero.Redondear(Subtotal - Descuento);

    private EstadoPedidoBase EstadoActual => EstadosPedido.De(Estado);
    public bool PermiteEdicion => EstadoActual.PermiteEdicion;

    private Pedido() { }

    internal Pedido(int clienteId, string clienteNombre, string email, string telefono, CategoriaCliente categoria,
        TipoEntrega entrega, IEnumerable<PedidoDetalle> lineas, DateTime fecha, string creadoPor)
    {
        ClienteId = clienteId;
        ClienteNombre = clienteNombre;
        ClienteEmail = email;
        ClienteTelefono = telefono;
        CategoriaCliente = categoria;
        TipoEntrega = entrega;
        Estado = EstadoPedido.BORRADOR;
        FechaCreacion = fecha;
        CreadoPor = creadoPor;
        _lineas.AddRange(lineas);
        Subtotal = SubtotalCalculado;
        Total = Subtotal;
    }

    //edicion del borrador

    public void AgregarLinea(IElementoCatalogo elemento, int cantidad)
    {
        EstadoActual.ExigirEdicion();
        var existente = _lineas.FirstOrDefault(l => string.Equals(l.Codigo, elemento.Codigo, StringComparison.OrdinalIgnoreCase));
        if (existente is not null) existente.CambiarCantidad(existente.Cantidad + Guardia.CantidadPositiva(cantidad));
        else _lineas.Add(PedidoDetalle.DesdeElemento(elemento, cantidad));
        InvalidarValoracion();
    }

    public void CambiarCantidad(string codigo, int cantidad)
    {
        EstadoActual.ExigirEdicion();
        ObtenerLinea(codigo).CambiarCantidad(cantidad);
        InvalidarValoracion();
    }

    //invariante: un pedido siempre tiene al menos una linea (quitar la ultima = vaciar el carrito)
    public void QuitarLinea(string codigo)
    {
        EstadoActual.ExigirEdicion();
        var linea = ObtenerLinea(codigo);
        if (_lineas.Count == 1)
            throw new ExcepcionDominio("PEDIDO_SIN_LINEAS", "Un pedido no puede quedar sin líneas: vacíe el carrito en su lugar.");
        _lineas.Remove(linea);
        InvalidarValoracion();
    }

    //re-toma la instantanea de precio/composicion de una linea (solo en BORRADOR)
    public void ActualizarPrecioLinea(IElementoCatalogo elementoActual)
    {
        EstadoActual.ExigirEdicion();
        var linea = ObtenerLinea(elementoActual.Codigo);
        var indice = _lineas.IndexOf(linea);
        _lineas[indice] = PedidoDetalle.DesdeElemento(elementoActual, linea.Cantidad);
        InvalidarValoracion();
    }

    public void CambiarEntrega(TipoEntrega entrega)
    {
        EstadoActual.ExigirEdicion();
        TipoEntrega = entrega;
        InvalidarValoracion();
    }

    public void CambiarCliente(Cliente cliente)
    {
        EstadoActual.ExigirEdicion();
        AsignarCliente(cliente.Id, cliente.Nombre, cliente.Email, cliente.Telefono, cliente.Categoria);
        InvalidarValoracion();
    }

    //cambia el cliente a partir de sus datos (borrador sin entidad Cliente a mano)
    public void CambiarCliente(int id, string nombre, string email, string telefono, CategoriaCliente categoria)
    {
        EstadoActual.ExigirEdicion();
        AsignarCliente(id, Guardia.Texto(nombre, "cliente", 120), email, telefono, categoria);
        InvalidarValoracion();
    }

    internal void AsignarCliente(int id, string nombre, string email, string telefono, CategoriaCliente categoria)
    {
        ClienteId = id;
        ClienteNombre = nombre;
        ClienteEmail = email;
        ClienteTelefono = telefono;
        CategoriaCliente = categoria;
    }

    private PedidoDetalle ObtenerLinea(string codigo) =>
        _lineas.FirstOrDefault(l => string.Equals(l.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
        ?? throw new ExcepcionDominio("LINEA_NO_EXISTE", $"El pedido no contiene la línea {codigo}.");

    private void InvalidarValoracion()
    {
        Subtotal = SubtotalCalculado;
        Descuento = 0;
        PoliticaDescuento = "NORMAL";
        TarifaEnvio = 0;
        Total = Subtotal;
    }

    //valoracion (Strategy + tarifa de la familia de entrega)

    public ResultadoDescuento AplicarValoracion(ICalculadoraDescuento calculadora, decimal tarifaEnvio)
    {
        if (!EstadoActual.PermiteValoracion)
            throw new ExcepcionDominio("TRANSICION_INVALIDA", $"No se puede recalcular un pedido en estado {Estado}.");
        Subtotal = SubtotalCalculado;
        var resultado = calculadora.Calcular(new ContextoDescuento(CategoriaCliente, UnidadesFisicas, Subtotal));
        Descuento = resultado.Monto;
        PoliticaDescuento = resultado.Politica;
        TarifaEnvio = Dinero.Redondear(Guardia.NoNegativo(tarifaEnvio, "tarifa"));
        Total = Dinero.Redondear(Subtotal - Descuento + TarifaEnvio);
        return resultado;
    }

    //requerimiento de stock expandido a SKU finales (cantidad de linea x componente)
    public IReadOnlyDictionary<string, int> RequerimientoPorSku() =>
        _lineas.SelectMany(l => l.Componentes.Select(c => (c.Sku, Cantidad: c.CantidadPorUnidad * l.Cantidad)))
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad), StringComparer.OrdinalIgnoreCase);

    //transiciones (State)

    public void Validar(DateTime fecha, string usuario)
    {
        if (_lineas.Count == 0) throw new ExcepcionDominio("PEDIDO_SIN_LINEAS", "El pedido no tiene líneas.");
        CambiarEstado(EstadoActual.Validar(), fecha, usuario, "Validación superada");
    }

    public void RegistrarPago(string claveIdempotencia, DateTime fecha, string usuario)
    {
        var siguiente = EstadoActual.Pagar();
        ClaveIdempotencia = Guardia.Texto(claveIdempotencia, "clave de idempotencia", 100);
        FechaPago = fecha;
        CambiarEstado(siguiente, fecha, usuario, $"Cobro aprobado ({claveIdempotencia})");
    }

    public void Despachar(DateTime fecha, string usuario, string guia) =>
        CambiarEstado(EstadoActual.Despachar(), fecha, usuario, $"Guía {guia}");

    public void Entregar(DateTime fecha, string usuario)
    {
        var siguiente = EstadoActual.Entregar();
        FechaEntrega = fecha;
        CambiarEstado(siguiente, fecha, usuario, "Entrega confirmada");
    }

    public void Cancelar(DateTime fecha, string usuario, string motivo) =>
        CambiarEstado(EstadoActual.Cancelar(), fecha, usuario, $"Cancelado: {motivo}");

    public void ExigirDevolucionPosible()
    {
        EstadoActual.ExigirDevolucionPosible();
        if (Devuelto) throw new ExcepcionDominio("DEVOLUCION_DUPLICADA", $"El pedido {Numero} ya fue devuelto.");
        if (FechaEntrega is null) throw new ExcepcionDominio("SIN_FECHA_ENTREGA", "El pedido no registra fecha de entrega.");
    }

    public void MarcarDevuelto(DateTime fecha, string usuario, string detalle)
    {
        ExigirDevolucionPosible();
        Devuelto = true;
        Version++;
        _historial.Add(new PedidoHistorialEstado(Estado, Estado, fecha, usuario, $"Devolución total aceptada: {detalle}"));
    }

    private void CambiarEstado(EstadoPedido nuevo, DateTime fecha, string usuario, string detalle)
    {
        var anterior = Estado;
        Estado = nuevo;
        Version++;
        _historial.Add(new PedidoHistorialEstado(anterior, nuevo, fecha, usuario, detalle));
    }

    //memento

    public PedidoMemento CrearMemento() =>
        new(ClienteId, ClienteNombre, ClienteEmail, ClienteTelefono, CategoriaCliente, TipoEntrega,
            _lineas.Select(l => l.Clonar()).ToList());

    //restaura un estado completo del borrador
    //no puede revertir un pedido cobrado
    public void Restaurar(PedidoMemento memento)
    {
        EstadoActual.ExigirEdicion();
        if (memento.Lineas.Count == 0)
            throw new ExcepcionDominio("PEDIDO_SIN_LINEAS", "El estado a restaurar no tiene líneas.");
        AsignarCliente(memento.ClienteId, memento.ClienteNombre, memento.ClienteEmail, memento.ClienteTelefono, memento.Categoria);
        TipoEntrega = memento.TipoEntrega;
        _lineas.Clear();
        _lineas.AddRange(memento.Lineas.Select(l => l.Clonar()));
        InvalidarValoracion();
    }

    //reconstruye un BORRADOR nuevo desde un estado guardado (deshacer tras vaciar el carrito)
    public static Pedido DesdeMemento(PedidoMemento memento, DateTime fecha, string usuario)
    {
        if (memento.Lineas.Count == 0)
            throw new ExcepcionDominio("PEDIDO_SIN_LINEAS", "El estado a restaurar no tiene líneas.");
        return new Pedido(memento.ClienteId, memento.ClienteNombre, memento.ClienteEmail, memento.ClienteTelefono,
            memento.Categoria, memento.TipoEntrega, memento.Lineas.Select(l => l.Clonar()), fecha, usuario);
    }

    //prototype

    //clona el pedido como un BORRADOR nuevo:
    //copia profunda de lineas y componentes, SIN identificador, sin pago, sin clave de idempotencia ni historial
    public Pedido Clonar()
    {
        var copia = new Pedido(ClienteId, ClienteNombre, ClienteEmail, ClienteTelefono, CategoriaCliente,
            TipoEntrega, _lineas.Select(l => l.Clonar()), FechaCreacion, CreadoPor);
        return copia;
    }

    public Pedido ClonarComoBorrador(DateTime fecha, string usuario)
    {
        var copia = Clonar();
        copia.FechaCreacion = fecha;
        copia.CreadoPor = usuario;
        return copia;
    }

    //reemplaza el contenido editable con el de otro borrador (guardar ediciones del carrito)
    public void ReemplazarContenido(Pedido otro)
    {
        EstadoActual.ExigirEdicion();
        Restaurar(otro.CrearMemento());
    }
}

//historial de cambios de estado (trazabilidad)
public class PedidoHistorialEstado
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public EstadoPedido? EstadoAnterior { get; private set; }
    public EstadoPedido EstadoNuevo { get; private set; }
    public DateTime Fecha { get; private set; }
    public string Usuario { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;

    private PedidoHistorialEstado() { }

    internal PedidoHistorialEstado(EstadoPedido? anterior, EstadoPedido nuevo, DateTime fecha, string usuario, string detalle)
    {
        EstadoAnterior = anterior;
        EstadoNuevo = nuevo;
        Fecha = fecha;
        Usuario = usuario;
        Detalle = detalle;
    }
}
