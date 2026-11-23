namespace LogiMarket.Application.Comun;

//resultado estructurado de una operacion de aplicacion (exito o rechazo con codigo)
public class ResultadoOperacion
{
    public bool Exito { get; init; }
    public string Codigo { get; init; } = "OK";
    public string Mensaje { get; init; } = string.Empty;

    public static ResultadoOperacion Ok(string mensaje) => new() { Exito = true, Codigo = "OK", Mensaje = mensaje };
    public static ResultadoOperacion Fallo(string codigo, string mensaje) => new() { Exito = false, Codigo = codigo, Mensaje = mensaje };

    public override string ToString() => $"{(Exito ? "ÉXITO" : "RECHAZO")} [{Codigo}] {Mensaje}";
}

public sealed class ResultadoOperacion<T> : ResultadoOperacion
{
    public T? Valor { get; init; }

    public static ResultadoOperacion<T> Ok(T valor, string mensaje) => new() { Exito = true, Codigo = "OK", Mensaje = mensaje, Valor = valor };
    public new static ResultadoOperacion<T> Fallo(string codigo, string mensaje) => new() { Exito = false, Codigo = codigo, Mensaje = mensaje };
}
