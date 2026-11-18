using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Composite: kit que agrupa productos u otros kits. No expone su colección interna mutable;
/// el recorrido se hace con <see cref="IIteradorComponentes"/> o con un visitante.
/// </summary>
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

    /// <summary>Agrega un hijo rechazando referencias circulares directas o indirectas.</summary>
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

    /// <summary>Hijos directos en una copia de solo lectura (para iteradores y visitantes).</summary>
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
    /// <summary>Expansión a SKU finales: SKU → cantidad por unidad del elemento (usa el Iterator).</summary>
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
