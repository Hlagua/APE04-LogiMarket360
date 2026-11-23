using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Entrega;

//todo producto de una familia de entrega declara su familia para impedir mezclas
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

//patron abstract factory (creacional)
//interfaz IFabricaEntrega
//objetivo: cada fabrica crea una FAMILIA coherente (tarifa, plazo, guia)
//el caso de uso trabaja solo con esta interfaz
public interface IFabricaEntrega
{
    TipoEntrega Tipo { get; }
    string Familia { get; }
    ICalculadoraTarifa CrearCalculadoraTarifa();
    ICalculadoraPlazo CrearCalculadoraPlazo();
    IGeneradorGuia CrearGeneradorGuia();
}

//familia ESTANDAR

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
        public decimal Calcular(decimal pesoKg) => ParametrosNegocio.TarifaEstandar;
    }

    private sealed class PlazoEstandar : ICalculadoraPlazo
    {
        public string Familia => NombreFamilia;
        public int Dias => ParametrosNegocio.PlazoEstandarDias;
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

//familia EXPRES

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
        public decimal Calcular(decimal pesoKg) => ParametrosNegocio.TarifaExpres;
    }

    private sealed class PlazoExpres : ICalculadoraPlazo
    {
        public string Familia => NombreFamilia;
        public int Dias => ParametrosNegocio.PlazoExpresDias;
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

//paquete coherente producido por UNA sola fabrica
public sealed record PaqueteEntrega(string Familia, decimal Tarifa, int PlazoDias, ICalculadoraPlazo Plazo, IGeneradorGuia Generador);

//selecciona la fabrica concreta segun el tipo de entrega del pedido
public sealed class ProveedorFabricasEntrega : IProveedorFabricasEntrega
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
