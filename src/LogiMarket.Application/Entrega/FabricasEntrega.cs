using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Entrega;

/// <summary>Todo producto de una familia de entrega declara su familia para impedir mezclas.</summary>
public interface IProductoEntrega
{
    string Familia { get; }
}

public interface ICalculadoraTarifa : IProductoEntrega
{
    decimal Calcular(decimal pesoKg);
}

public interface ICalculadoraPlazo : IProductoEntrega
{
    int Dias { get; }
    DateTime EstimarEntrega(DateTime desde);
}

public sealed record GuiaEnvio(string Numero, string Familia, string Etiqueta, int PlazoDias, DateTime FechaEstimada);

public interface IGeneradorGuia : IProductoEntrega
{
    GuiaEnvio Generar(int pedidoId, string cliente, decimal pesoKg, DateTime fecha, ICalculadoraPlazo plazo);
}

/// <summary>
/// Patrón Abstract Factory: cada fábrica crea una FAMILIA coherente (tarifa, plazo, guía).
/// El caso de uso trabaja solo con esta interfaz.
/// </summary>
public interface IFabricaEntrega
{
    TipoEntrega Tipo { get; }
    string Familia { get; }
    ICalculadoraTarifa CrearCalculadoraTarifa();
    ICalculadoraPlazo CrearCalculadoraPlazo();
    IGeneradorGuia CrearGeneradorGuia();
}

// ---------------- Familia ESTÁNDAR ----------------

public sealed class EntregaEstandarFactory : IFabricaEntrega
{
    public const string NombreFamilia = "ESTANDAR";
    public TipoEntrega Tipo => TipoEntrega.ESTANDAR;
    public string Familia => NombreFamilia;
    public ICalculadoraTarifa CrearCalculadoraTarifa() => new TarifaEstandar();
    public ICalculadoraPlazo CrearCalculadoraPlazo() => new PlazoEstandar();
    public IGeneradorGuia CrearGeneradorGuia() => new GuiaEstandar();

    private sealed class TarifaEstandar : ICalculadoraTarifa
    {
        public string Familia => NombreFamilia;
        public decimal Calcular(decimal pesoKg) => 5.00m;
    }

    private sealed class PlazoEstandar : ICalculadoraPlazo
    {
        public string Familia => NombreFamilia;
        public int Dias => 3;
        public DateTime EstimarEntrega(DateTime desde) => desde.Date.AddDays(Dias);
    }

    private sealed class GuiaEstandar : IGeneradorGuia
    {
        public string Familia => NombreFamilia;

        public GuiaEnvio Generar(int pedidoId, string cliente, decimal pesoKg, DateTime fecha, ICalculadoraPlazo plazo)
        {
            FamiliaCoherente.Exigir(this, plazo);
            var numero = $"STD-{fecha:yyyyMMdd}-{pedidoId:000000}";
            var etiqueta = $"[ENVIO ESTANDAR] Guía {numero} | Destinatario: {cliente} | Peso {Dinero.Formatear(pesoKg)} kg | " +
                           $"Plazo {plazo.Dias} días | Llegada estimada {plazo.EstimarEntrega(fecha):yyyy-MM-dd}";
            return new GuiaEnvio(numero, Familia, etiqueta, plazo.Dias, plazo.EstimarEntrega(fecha));
        }
    }
}

// ---------------- Familia EXPRÉS ----------------

public sealed class EntregaExpresFactory : IFabricaEntrega
{
    public const string NombreFamilia = "EXPRES";
    public TipoEntrega Tipo => TipoEntrega.EXPRES;
    public string Familia => NombreFamilia;
    public ICalculadoraTarifa CrearCalculadoraTarifa() => new TarifaExpres();
    public ICalculadoraPlazo CrearCalculadoraPlazo() => new PlazoExpres();
    public IGeneradorGuia CrearGeneradorGuia() => new GuiaExpres();

    private sealed class TarifaExpres : ICalculadoraTarifa
    {
        public string Familia => NombreFamilia;
        public decimal Calcular(decimal pesoKg) => 12.00m;
    }

    private sealed class PlazoExpres : ICalculadoraPlazo
    {
        public string Familia => NombreFamilia;
        public int Dias => 1;
        public DateTime EstimarEntrega(DateTime desde) => desde.Date.AddDays(Dias);
    }

    private sealed class GuiaExpres : IGeneradorGuia
    {
        public string Familia => NombreFamilia;

        public GuiaEnvio Generar(int pedidoId, string cliente, decimal pesoKg, DateTime fecha, ICalculadoraPlazo plazo)
        {
            FamiliaCoherente.Exigir(this, plazo);
            var numero = $"EXP-{fecha:yyyyMMdd}-{pedidoId:000000}";
            var etiqueta = $"*** EXPRES - PRIORIDAD ALTA *** Guía {numero} | {cliente} | {Dinero.Formatear(pesoKg)} kg | " +
                           $"Entrega en {plazo.Dias} día ({plazo.EstimarEntrega(fecha):yyyy-MM-dd})";
            return new GuiaEnvio(numero, Familia, etiqueta, plazo.Dias, plazo.EstimarEntrega(fecha));
        }
    }
}

internal static class FamiliaCoherente
{
    public static void Exigir(IProductoEntrega a, IProductoEntrega b)
    {
        if (a.Familia != b.Familia)
            throw new ExcepcionDominio("FAMILIA_MEZCLADA",
                $"No se pueden mezclar productos de entrega {a.Familia} y {b.Familia}.");
    }
}

/// <summary>Paquete coherente producido por UNA sola fábrica.</summary>
public sealed record PaqueteEntrega(string Familia, decimal Tarifa, int PlazoDias, ICalculadoraPlazo Plazo, IGeneradorGuia Generador);

/// <summary>Selecciona la fábrica concreta según el tipo de entrega del pedido.</summary>
public sealed class ProveedorFabricasEntrega
{
    private readonly IReadOnlyDictionary<TipoEntrega, IFabricaEntrega> _fabricas;

    public ProveedorFabricasEntrega(IEnumerable<IFabricaEntrega> fabricas) =>
        _fabricas = fabricas.ToDictionary(f => f.Tipo);

    public IFabricaEntrega Para(TipoEntrega tipo) =>
        _fabricas.TryGetValue(tipo, out var f)
            ? f
            : throw new ExcepcionDominio("ENTREGA_NO_SOPORTADA", $"No hay familia de entrega para {tipo}.");

    public PaqueteEntrega Preparar(TipoEntrega tipo, decimal pesoKg)
    {
        var fabrica = Para(tipo);
        var plazo = fabrica.CrearCalculadoraPlazo();
        return new PaqueteEntrega(fabrica.Familia, fabrica.CrearCalculadoraTarifa().Calcular(pesoKg), plazo.Dias,
            plazo, fabrica.CrearGeneradorGuia());
    }
}
