using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Promociones;

/// <summary>Datos que una política necesita para decidir si aplica.</summary>
public sealed record ContextoDescuento(CategoriaCliente Categoria, int UnidadesFisicas, decimal Subtotal);

public sealed record ResultadoDescuento(string Politica, decimal Tasa, decimal Monto);

/// <summary>
/// Patrón Strategy: cada promoción encapsula su condición y su tasa. La prioridad es explícita:
/// gana la política aplicable de MAYOR prioridad y nunca se acumulan descuentos.
/// </summary>
public interface IPoliticaDescuento
{
    string Codigo { get; }
    int Prioridad { get; }
    decimal Tasa { get; }
    bool Aplica(ContextoDescuento contexto);
}

/// <summary>NORMAL: 0 %. Siempre aplica y tiene la menor prioridad (política por defecto).</summary>
public sealed class SinDescuento : IPoliticaDescuento
{
    public string Codigo => "NORMAL";
    public int Prioridad => 0;
    public decimal Tasa => 0m;
    public bool Aplica(ContextoDescuento contexto) => true;
}

/// <summary>VIP: 10 % si el cliente pertenece a la categoría VIP.</summary>
public sealed class Vip10 : IPoliticaDescuento
{
    public string Codigo => "VIP";
    public int Prioridad => 10;
    public decimal Tasa => 0.10m;
    public bool Aplica(ContextoDescuento contexto) => contexto.Categoria == CategoriaCliente.VIP;
}

/// <summary>MAYORISTA: 15 % si hay al menos 10 unidades físicas finales, sin importar la categoría.</summary>
public sealed class Mayorista15 : IPoliticaDescuento
{
    public const int UnidadesMinimas = 10;
    public string Codigo => "MAYORISTA";
    public int Prioridad => 20;
    public decimal Tasa => 0.15m;
    public bool Aplica(ContextoDescuento contexto) => contexto.UnidadesFisicas >= UnidadesMinimas;
}

/// <summary>
/// Contexto del Strategy. Depende solo de la interfaz: agregar una política nueva = registrar otra
/// implementación, sin editar ninguna cadena de condiciones aquí (OCP).
/// </summary>
public sealed class CalculadoraDescuento
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
