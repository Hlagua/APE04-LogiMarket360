namespace LogiMarket.Domain.Comun;

//violacion de una invariante o regla de negocio
//lleva un codigo estable para pruebas y UI
public class ExcepcionDominio : Exception
{
    public string Codigo { get; }

    public ExcepcionDominio(string codigo, string mensaje) : base(mensaje) => Codigo = codigo;
}
