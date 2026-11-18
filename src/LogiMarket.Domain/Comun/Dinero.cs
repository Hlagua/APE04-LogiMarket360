namespace LogiMarket.Domain.Comun;

/// <summary>Regla única de redondeo monetario (DRY): 2 decimales, MidpointRounding.AwayFromZero.</summary>
public static class Dinero
{
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static string Formatear(decimal valor) =>
        Redondear(valor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
