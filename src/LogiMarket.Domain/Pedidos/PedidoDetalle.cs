using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos;

/// <summary>
/// Línea de venta con INSTANTÁNEA del precio y de la composición del kit al momento de registrarla.
/// Cambios posteriores del catálogo no la alteran.
/// </summary>
public class PedidoDetalle : IPrototipo<PedidoDetalle>
{
    private readonly List<PedidoDetalleComponente> _componentes = new();

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool EsKit { get; private set; }
    public int Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public IReadOnlyCollection<PedidoDetalleComponente> Componentes => _componentes.AsReadOnly();

    public decimal Subtotal => Dinero.Redondear(_componentes.Sum(c => c.PrecioUnitario * c.CantidadPorUnidad) * Cantidad);
    public int UnidadesFisicas => _componentes.Sum(c => c.CantidadPorUnidad) * Cantidad;
    public decimal PesoKg => _componentes.Sum(c => c.PesoKg * c.CantidadPorUnidad) * Cantidad;

    private PedidoDetalle() { }

    internal static PedidoDetalle DesdeElemento(IElementoCatalogo elemento, int cantidad)
    {
        var d = new PedidoDetalle
        {
            Codigo = elemento.Codigo,
            Descripcion = elemento.Nombre,
            EsKit = elemento.EsCompuesto,
            Cantidad = Guardia.CantidadPositiva(cantidad),
            PrecioUnitario = elemento.Precio
        };
        // Iterator: expansión determinista a SKU finales para la instantánea.
        foreach (var grupo in elemento.ComponentesFisicos().GroupBy(c => c.Sku))
        {
            var primero = grupo.First();
            d._componentes.Add(new PedidoDetalleComponente(primero.Sku, primero.Nombre,
                grupo.Sum(c => c.Cantidad), primero.PrecioUnitario, primero.PesoKg));
        }
        return d;
    }

    internal void CambiarCantidad(int cantidad) => Cantidad = Guardia.CantidadPositiva(cantidad);

    /// <summary>Prototype: copia profunda; la colección de componentes es una referencia distinta.</summary>
    public PedidoDetalle Clonar()
    {
        var copia = new PedidoDetalle
        {
            Codigo = Codigo,
            Descripcion = Descripcion,
            EsKit = EsKit,
            Cantidad = Cantidad,
            PrecioUnitario = PrecioUnitario
        };
        copia._componentes.AddRange(_componentes.Select(c => c.Clonar()));
        return copia;
    }
}

/// <summary>Instantánea de un SKU final dentro de una línea (cantidad por UNA unidad de la línea).</summary>
public class PedidoDetalleComponente : IPrototipo<PedidoDetalleComponente>
{
    public int Id { get; private set; }
    public int PedidoDetalleId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public int CantidadPorUnidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal PesoKg { get; private set; }

    private PedidoDetalleComponente() { }

    internal PedidoDetalleComponente(string sku, string nombre, int cantidadPorUnidad, decimal precioUnitario, decimal pesoKg)
    {
        Sku = sku;
        Nombre = nombre;
        CantidadPorUnidad = Guardia.CantidadPositiva(cantidadPorUnidad);
        PrecioUnitario = precioUnitario;
        PesoKg = pesoKg;
    }

    public PedidoDetalleComponente Clonar() => new(Sku, Nombre, CantidadPorUnidad, PrecioUnitario, PesoKg);
}
