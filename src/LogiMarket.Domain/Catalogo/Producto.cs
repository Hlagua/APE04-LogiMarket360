using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Producto de catálogo persistido (valores ACTUALES). Las ventas guardan instantáneas aparte.
/// Version es token de concurrencia optimista contra la sobreventa.
/// </summary>
public class Producto
{
    public int Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public decimal Precio { get; private set; }
    public decimal PesoKg { get; private set; }
    public int Stock { get; private set; }
    public bool Activo { get; private set; }
    public int Version { get; private set; }

    private Producto() { }

    public static Producto Crear(string sku, string nombre, string categoria, string descripcion,
        decimal precio, decimal pesoKg, int stockInicial)
    {
        var p = new Producto
        {
            Sku = NormalizarSku(sku),
            Stock = Guardia.EnteroNoNegativo(stockInicial, "stock"),
            Activo = true
        };
        p.Actualizar(nombre, categoria, descripcion, precio, pesoKg);
        return p;
    }

    public static string NormalizarSku(string sku) => Guardia.Texto(sku, "SKU", 30).ToUpperInvariant();

    public void Actualizar(string nombre, string categoria, string descripcion, decimal precio, decimal pesoKg)
    {
        Nombre = Guardia.Texto(nombre, "nombre", 120);
        Categoria = Guardia.Texto(categoria, "categoría", 60);
        Descripcion = (descripcion ?? string.Empty).Trim();
        Precio = Guardia.PrecioPositivo(precio);
        PesoKg = Guardia.NoNegativo(pesoKg, "peso");
        Version++;
    }

    public void DescontarStock(int cantidad)
    {
        Guardia.CantidadPositiva(cantidad);
        if (cantidad > Stock)
            throw new ExcepcionDominio("STOCK_INSUFICIENTE",
                $"Stock insuficiente para {Sku}: requerido {cantidad}, disponible {Stock}.");
        Stock -= cantidad;
        Version++;
    }

    public void ReintegrarStock(int cantidad)
    {
        Guardia.CantidadPositiva(cantidad);
        Stock += cantidad;
        Version++;
    }

    /// <summary>Ajuste manual (ADMIN). Devuelve la diferencia aplicada para registrar el movimiento.</summary>
    public int AjustarStock(int nuevoStock)
    {
        Guardia.EnteroNoNegativo(nuevoStock, "stock");
        var diferencia = nuevoStock - Stock;
        Stock = nuevoStock;
        Version++;
        return diferencia;
    }

    public void Desactivar() { Activo = false; Version++; }
    public void Activar() { Activo = true; Version++; }

    public ProductoSimple ComoElemento() => new(Sku, Nombre, Categoria, Precio, PesoKg);
}
