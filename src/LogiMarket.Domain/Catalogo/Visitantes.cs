using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

//patron visitor (comportamental)
//interfaz IVisitanteCatalogo
//objetivo: operaciones sobre la estructura Composite sin modificar sus clases
public interface IVisitanteCatalogo
{
    void VisitarProducto(ProductoSimple producto, int multiplicador);
    void VisitarKit(KitCompuesto kit, int multiplicador);
    void SalirKit(KitCompuesto kit);
}

public sealed record LineaDesglose(string Sku, string Nombre, int Cantidad, int Nivel);

//visitante 1: desglose fisico por SKU, conservando el nivel de anidamiento visitado
public sealed class DesgloseSkuVisitor : IVisitanteCatalogo
{
    private readonly List<LineaDesglose> _lineas = new();
    private readonly List<string> _kitsVisitados = new();
    private int _nivel;

    public IReadOnlyList<LineaDesglose> Lineas => _lineas;
    public IReadOnlyList<string> KitsVisitados => _kitsVisitados;

    public IReadOnlyDictionary<string, int> TotalesPorSku =>
        _lineas.GroupBy(l => l.Sku).ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));

    public void VisitarProducto(ProductoSimple producto, int multiplicador) =>
        _lineas.Add(new LineaDesglose(producto.Codigo, producto.Nombre, multiplicador, _nivel));

    public void VisitarKit(KitCompuesto kit, int multiplicador)
    {
        _kitsVisitados.Add($"{new string('>', _nivel)}{kit.Codigo} x{multiplicador}");
        _nivel++;
    }

    public void SalirKit(KitCompuesto kit) => _nivel--;
}

//visitante 2: estimacion del peso logistico total (kg) y kits anidados atravesados
public sealed class PesoLogisticoVisitor : IVisitanteCatalogo
{
    private decimal _peso;
    private int _nivel;

    public decimal PesoTotalKg => Math.Round(_peso, 2, MidpointRounding.AwayFromZero);
    public int KitsAtravesados { get; private set; }
    public int ProfundidadMaxima { get; private set; }

    public void VisitarProducto(ProductoSimple producto, int multiplicador) =>
        _peso += producto.PesoKg * multiplicador;

    public void VisitarKit(KitCompuesto kit, int multiplicador)
    {
        KitsAtravesados++;
        _nivel++;
        ProfundidadMaxima = Math.Max(ProfundidadMaxima, _nivel);
    }

    public void SalirKit(KitCompuesto kit) => _nivel--;

    public string Resumen =>
        $"Peso logístico {Dinero.Formatear(PesoTotalKg)} kg; kits atravesados {KitsAtravesados}; profundidad {ProfundidadMaxima}.";
}
