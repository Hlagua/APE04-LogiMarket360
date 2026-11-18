namespace LogiMarket.Domain.Comun;

/// <summary>Violación de una invariante o regla de negocio. Lleva un código estable para pruebas y UI.</summary>
public class ExcepcionDominio : Exception
{
    public string Codigo { get; }

    public ExcepcionDominio(string codigo, string mensaje) : base(mensaje) => Codigo = codigo;
}
