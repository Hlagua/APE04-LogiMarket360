using System.Collections.Concurrent;
using LogiMarket.Application.Consultas;

namespace LogiMarket.Application.Catalogo;

/// <summary>Estado INTRÍNSECO compartido (inmutable) del patrón Flyweight.</summary>
public sealed class MetadatosSku
{
    public string Sku { get; }
    public string Nombre { get; }
    public string Categoria { get; }
    public string Descripcion { get; }

    internal MetadatosSku(string sku, string nombre, string categoria, string descripcion)
    {
        Sku = sku;
        Nombre = nombre;
        Categoria = categoria;
        Descripcion = descripcion;
    }
}

/// <summary>Fila de vista: metadatos compartidos + estado EXTRÍNSECO por fila (stock, cantidad, precio).</summary>
public sealed record FilaCatalogoVisual(MetadatosSku Metadatos, int Stock, int Cantidad, decimal PrecioVenta)
{
    public string Sku => Metadatos.Sku;
    public string Nombre => Metadatos.Nombre;
    public string Categoria => Metadatos.Categoria;
}

/// <summary>Fila equivalente SIN Flyweight (cada fila duplica sus metadatos), usada solo para comparar.</summary>
public sealed record FilaSinFlyweight(string Sku, string Nombre, string Categoria, string Descripcion, int Stock, int Cantidad, decimal PrecioVenta);

/// <summary>Fábrica Flyweight: entrega la misma instancia de metadatos para un mismo SKU.</summary>
public sealed class CatalogoVisualFlyweightFactory
{
    private readonly ConcurrentDictionary<string, MetadatosSku> _cache = new(StringComparer.OrdinalIgnoreCase);
    private int _solicitudes;

    public int InstanciasCreadas => _cache.Count;
    public int Solicitudes => _solicitudes;

    public MetadatosSku Obtener(string sku, string nombre, string categoria, string descripcion)
    {
        Interlocked.Increment(ref _solicitudes);
        return _cache.GetOrAdd(sku, s => new MetadatosSku(s, nombre, categoria, descripcion));
    }

    public void Limpiar()
    {
        _cache.Clear();
        _solicitudes = 0;
    }
}

public sealed record MedicionFlyweight(int Filas, int SkusDistintos, int InstanciasMetadatos, bool ReferenciasCompartidas,
    long BytesConFlyweight, long BytesSinFlyweight, string Condiciones)
{
    public double AhorroPorcentual => BytesSinFlyweight == 0 ? 0 : 100.0 * (BytesSinFlyweight - BytesConFlyweight) / BytesSinFlyweight;
}

/// <summary>
/// Genera la vista masiva (≥1.000 filas) y mide bajo condiciones EQUIVALENTES: mismos datos de origen,
/// mismo número de filas, mismas copias de cadenas simulando la lectura desde la base, medido con
/// GC.GetAllocatedBytesForCurrentThread en el mismo hilo.
/// </summary>
public sealed class GeneradorVistaMasiva
{
    private readonly CatalogoVisualFlyweightFactory _fabrica;

    public GeneradorVistaMasiva(CatalogoVisualFlyweightFactory fabrica) => _fabrica = fabrica;

    public IReadOnlyList<FilaCatalogoVisual> GenerarConFlyweight(IReadOnlyList<ProductoDto> productos, int filas)
    {
        var lista = new List<FilaCatalogoVisual>(filas);
        for (var i = 0; i < filas; i++)
        {
            var p = productos[i % productos.Count];
            var metadatos = _fabrica.Obtener(p.Sku, Copia(p.Nombre), Copia(p.Categoria), Copia(p.Descripcion));
            lista.Add(new FilaCatalogoVisual(metadatos, p.Stock, 1 + i % 5, p.Precio));
        }
        return lista;
    }

    public static IReadOnlyList<FilaSinFlyweight> GenerarSinFlyweight(IReadOnlyList<ProductoDto> productos, int filas)
    {
        var lista = new List<FilaSinFlyweight>(filas);
        for (var i = 0; i < filas; i++)
        {
            var p = productos[i % productos.Count];
            lista.Add(new FilaSinFlyweight(Copia(p.Sku), Copia(p.Nombre), Copia(p.Categoria), Copia(p.Descripcion), p.Stock, 1 + i % 5, p.Precio));
        }
        return lista;
    }

    public MedicionFlyweight Medir(IReadOnlyList<ProductoDto> productos, int filas = 1000)
    {
        if (productos.Count == 0) throw new InvalidOperationException("No hay productos para generar la vista.");
        _fabrica.Limpiar();
        GenerarSinFlyweight(productos, 10); // calentamiento JIT de ambas rutas
        GenerarConFlyweight(productos, 10);
        _fabrica.Limpiar();

        // Se mide la memoria RETENIDA (lo que el patrón ahorra): GC completo antes y después.
        var antesSin = GC.GetTotalMemory(true);
        IReadOnlyList<FilaSinFlyweight>? sin = GenerarSinFlyweight(productos, filas);
        var bytesSin = GC.GetTotalMemory(true) - antesSin;
        GC.KeepAlive(sin);
        sin = null;

        var antesCon = GC.GetTotalMemory(true);
        var con = GenerarConFlyweight(productos, filas);
        var bytesCon = GC.GetTotalMemory(true) - antesCon;

        // Identidad: todas las filas del mismo SKU comparten la MISMA instancia de metadatos.
        var compartidas = con.GroupBy(f => f.Sku).All(g => g.All(f => ReferenceEquals(f.Metadatos, g.First().Metadatos)));
        GC.KeepAlive(con);

        return new MedicionFlyweight(filas, productos.Count, _fabrica.InstanciasCreadas, compartidas, bytesCon, bytesSin,
            $"{filas} filas, {productos.Count} SKU, mismas copias de cadenas por fila en ambos escenarios, " +
            "memoria retenida medida con GC.GetTotalMemory(true) tras calentamiento JIT; valores aproximados.");
    }

    // Simula que cada fila llega con sus propias cadenas (como al materializar filas desde la base).
    private static string Copia(string s) => new(s.AsSpan());
}
