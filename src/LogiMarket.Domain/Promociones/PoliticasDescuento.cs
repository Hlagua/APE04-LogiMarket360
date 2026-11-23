using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Promociones;

//datos que una politica necesita para decidir si aplica
public sealed record ContextoDescuento(CategoriaCliente Categoria, int UnidadesFisicas, decimal Subtotal);

public sealed record ResultadoDescuento(string Politica, decimal Tasa, decimal Monto);

//patron strategy (comportamental)
//interfaz IPoliticaDescuento
//objetivo: cada promocion encapsula su condicion y su tasa
//la prioridad es explicita: gana la politica aplicable de MAYOR prioridad
//nunca se acumulan descuentos
public interface IPoliticaDescuento
{
    string Codigo { get; }
    int Prioridad { get; }
    decimal Tasa { get; }
    bool Aplica(ContextoDescuento contexto);
}

//NORMAL: 0 %. Siempre aplica y tiene la menor prioridad (politica por defecto)
public sealed class SinDescuento : IPoliticaDescuento
{
    public string Codigo => "NORMAL";
    public int Prioridad => 0;
    public decimal Tasa => 0m;
    public bool Aplica(ContextoDescuento contexto) => true;
}

//VIP: 10 % si el cliente pertenece a la categoria VIP
public sealed class Vip10 : IPoliticaDescuento
{
    public string Codigo => "VIP";
    public int Prioridad => 10;
    public decimal Tasa => ParametrosNegocio.TasaVip;
    public bool Aplica(ContextoDescuento contexto) => contexto.Categoria == CategoriaCliente.VIP;
}

//MAYORISTA: 15 % si hay al menos 10 unidades fisicas finales, sin importar la categoria
public sealed class Mayorista15 : IPoliticaDescuento
{
    public const int UnidadesMinimas = ParametrosNegocio.UnidadesMinimasMayorista;
    public string Codigo => "MAYORISTA";
    public int Prioridad => 20;
    public decimal Tasa => ParametrosNegocio.TasaMayorista;
    public bool Aplica(ContextoDescuento contexto) => contexto.UnidadesFisicas >= UnidadesMinimas;
}

//contexto del Strategy
//depende solo de la interfaz:
//agregar una politica nueva = registrar otra implementacion, sin editar ninguna cadena de condiciones aqui (OCP)
public interface ICalculadoraDescuento
{
    IReadOnlyList<IPoliticaDescuento> Politicas { get; }
    IPoliticaDescuento Seleccionar(ContextoDescuento contexto);
    ResultadoDescuento Calcular(ContextoDescuento contexto);
}

public sealed class CalculadoraDescuento : ICalculadoraDescuento
{
    private readonly IReadOnlyList<IPoliticaDescuento> _politicas;

    public CalculadoraDescuento(IEnumerable<IPoliticaDescuento> politicas)
    {
        _politicas = politicas.OrderByDescending(p => p.Prioridad).ToList();
        if (_politicas.Count == 0)
            throw new ArgumentException("Debe existir al menos una política de descuento.", nameof(politicas));
    }

    public IReadOnlyList<IPoliticaDescuento> Politicas => _politicas;

    public IPoliticaDescuento Seleccionar(ContextoDescuento contexto) =>
        _politicas.FirstOrDefault(p => p.Aplica(contexto))
        ?? throw new ExcepcionDominio("SIN_POLITICA", "Ninguna política de descuento aplica.");

    public ResultadoDescuento Calcular(ContextoDescuento contexto)
    {
        var politica = Seleccionar(contexto);
        return new ResultadoDescuento(politica.Codigo, politica.Tasa, Dinero.Redondear(contexto.Subtotal * politica.Tasa));
    }

    public static CalculadoraDescuento PorDefecto() =>
        new(new IPoliticaDescuento[] { new SinDescuento(), new Vip10(), new Mayorista15() });
}
