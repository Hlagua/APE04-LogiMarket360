using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Construye la estructura Composite a partir de las entidades persistidas (Producto/Kit) y detecta
/// ciclos indirectos. Es una política pura del dominio, sin acceso a datos.
/// </summary>
public sealed class ArbolCatalogo
{
    private readonly Dictionary<string, Producto> _productos;
    private readonly Dictionary<string, Kit> _kits;
    private readonly Dictionary<string, IElementoCatalogo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ArbolCatalogo(IEnumerable<Producto> productos, IEnumerable<Kit> kits)
    {
        _productos = productos.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        _kits = kits.ToDictionary(k => k.Codigo, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<string> CodigosKits => _kits.Keys;

    public bool Existe(string codigo) => _productos.ContainsKey(codigo) || _kits.ContainsKey(codigo);

    public bool EsKit(string codigo) => _kits.ContainsKey(codigo);

    public IElementoCatalogo Obtener(string codigo) =>
        Obtener(codigo, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private IElementoCatalogo Obtener(string codigo, HashSet<string> enConstruccion)
    {
        if (_cache.TryGetValue(codigo, out var enCache)) return enCache;

        if (_productos.TryGetValue(codigo, out var producto))
            return _cache[codigo] = producto.ComoElemento();

        if (!_kits.TryGetValue(codigo, out var kit))
            throw new ExcepcionDominio("CATALOGO_NO_EXISTE", $"El código {codigo} no existe en el catálogo.");

        if (!enConstruccion.Add(kit.Codigo))
            throw new ExcepcionDominio("KIT_CICLO", $"Referencia circular detectada en el kit {kit.Codigo}.");

        if (kit.Componentes.Count == 0)
            throw new ExcepcionDominio("KIT_VACIO", $"El kit {kit.Codigo} no tiene componentes.");

        var compuesto = new KitCompuesto(kit.Codigo, kit.Nombre);
        foreach (var c in kit.Componentes)
            compuesto.Agregar(Obtener(c.CodigoComponente, enConstruccion), c.Cantidad);

        enConstruccion.Remove(kit.Codigo);
        return _cache[codigo] = compuesto;
    }

    /// <summary>Valida que todos los kits se puedan construir sin ciclos ni referencias rotas.</summary>
    public void ValidarIntegridad()
    {
        foreach (var codigo in _kits.Keys) Obtener(codigo);
    }
}
