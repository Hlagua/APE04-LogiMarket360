namespace LogiMarket.Domain.Comun;

/// <summary>Validaciones de invariantes reutilizables (evita validaciones dispersas).</summary>
public static class Guardia
{
    public static string Texto(string? valor, string campo, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ExcepcionDominio("CAMPO_REQUERIDO", $"El campo '{campo}' es obligatorio.");
        var limpio = valor.Trim();
        if (limpio.Length > max)
            throw new ExcepcionDominio("CAMPO_LARGO", $"El campo '{campo}' admite máximo {max} caracteres.");
        return limpio;
    }

    public static decimal PrecioPositivo(decimal precio)
    {
        if (precio <= 0) throw new ExcepcionDominio("PRECIO_INVALIDO", "El precio debe ser mayor que cero.");
        return Dinero.Redondear(precio);
    }

    public static decimal NoNegativo(decimal valor, string campo)
    {
        if (valor < 0) throw new ExcepcionDominio("VALOR_NEGATIVO", $"El campo '{campo}' no puede ser negativo.");
        return valor;
    }

    public static int EnteroNoNegativo(int valor, string campo)
    {
        if (valor < 0) throw new ExcepcionDominio("VALOR_NEGATIVO", $"El campo '{campo}' no puede ser negativo.");
        return valor;
    }

    public static int CantidadPositiva(int cantidad)
    {
        if (cantidad <= 0) throw new ExcepcionDominio("CANTIDAD_INVALIDA", "La cantidad debe ser un entero mayor que cero.");
        return cantidad;
    }
}
