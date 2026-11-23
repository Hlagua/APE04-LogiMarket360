namespace LogiMarket.Domain.Comun;

//regla unica de redondeo monetario (DRY): 2 decimales, MidpointRounding.AwayFromZero
public static class Dinero
{
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static string Formatear(decimal valor) =>
        Redondear(valor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
