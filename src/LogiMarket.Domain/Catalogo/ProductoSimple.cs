using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>Composite: hoja inmutable que representa un SKU físico.</summary>
public sealed class ProductoSimple : IElementoCatalogo
{
    public string Codigo { get; }
    public string Nombre { get; }
    public string Categoria { get; }
    public decimal Precio { get; }
    public decimal PesoKg { get; }
    public bool EsCompuesto => false;

    public ProductoSimple(string sku, string nombre, string categoria, decimal precio, decimal pesoKg)
    {
        Codigo = Producto.NormalizarSku(sku);
        Nombre = Guardia.Texto(nombre, "nombre", 120);
        Categoria = categoria ?? string.Empty;
        Precio = Guardia.PrecioPositivo(precio);
        PesoKg = Guardia.NoNegativo(pesoKg, "peso");
    }

    public bool Contiene(string codigo) => string.Equals(Codigo, codigo, StringComparison.OrdinalIgnoreCase);

    public void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1) =>
        visitante.VisitarProducto(this, multiplicador);

    public IIteradorComponentes CrearIterador() => new IteradorComponentesFisicos(this);
}
