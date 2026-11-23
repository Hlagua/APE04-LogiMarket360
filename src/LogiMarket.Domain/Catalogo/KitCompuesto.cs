using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

//composite: kit que agrupa productos u otros kits
//no expone su coleccion interna mutable
//el recorrido se hace con IIteradorComponentes o con un visitante
public sealed class KitCompuesto : IElementoCatalogo
{
    private readonly List<(IElementoCatalogo Elemento, int Cantidad)> _hijos = new();

    public string Codigo { get; }
    public string Nombre { get; }
    public bool EsCompuesto => true;

    public KitCompuesto(string codigo, string nombre)
    {
        Codigo = Producto.NormalizarSku(codigo);
        Nombre = Guardia.Texto(nombre, "nombre", 120);
    }

    public decimal Precio => Dinero.Redondear(_hijos.Sum(h => h.Elemento.Precio * h.Cantidad));

    public decimal PesoKg => _hijos.Sum(h => h.Elemento.PesoKg * h.Cantidad);

    public int CantidadHijos => _hijos.Count;

    //agrega un hijo rechazando referencias circulares directas o indirectas
    public KitCompuesto Agregar(IElementoCatalogo elemento, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(elemento);
        Guardia.CantidadPositiva(cantidad);
        if (elemento.Contiene(Codigo))
            throw new ExcepcionDominio("KIT_CICLO",
                $"Referencia circular: {elemento.Codigo} contiene (directa o indirectamente) a {Codigo}.");
        _hijos.Add((elemento, cantidad));
        return this;
    }

    public bool Contiene(string codigo) =>
        string.Equals(Codigo, codigo, StringComparison.OrdinalIgnoreCase) ||
        _hijos.Any(h => h.Elemento.Contiene(codigo));

    //hijos directos en una copia de solo lectura (para iteradores y visitantes)
    public IReadOnlyList<(IElementoCatalogo Elemento, int Cantidad)> Hijos => _hijos.ToArray();

    public void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1)
    {
        visitante.VisitarKit(this, multiplicador);
        foreach (var (elemento, cantidad) in _hijos)
            elemento.Aceptar(visitante, multiplicador * cantidad);
        visitante.SalirKit(this);
    }

    public IIteradorComponentes CrearIterador() => new IteradorComponentesFisicos(this);
}

public static class ElementoCatalogoExtensiones
{
    //expansion a SKU finales: SKU -> cantidad por unidad del elemento (usa el Iterator)
    public static IReadOnlyDictionary<string, int> ExpandirSkus(this IElementoCatalogo elemento)
    {
        var resultado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var it = elemento.CrearIterador();
        while (it.MoverSiguiente())
            resultado[it.Actual.Sku] = resultado.GetValueOrDefault(it.Actual.Sku) + it.Actual.Cantidad;
        return resultado;
    }

    public static IReadOnlyList<ComponenteFisico> ComponentesFisicos(this IElementoCatalogo elemento)
    {
        var lista = new List<ComponenteFisico>();
        var it = elemento.CrearIterador();
        while (it.MoverSiguiente()) lista.Add(it.Actual);
        return lista;
    }
}
