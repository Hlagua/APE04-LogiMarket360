namespace LogiMarket.Application.Comun;

//el usuario de la sesion no tiene permiso para la operacion (politica en Application)
public sealed class AccesoDenegadoException : Exception
{
    public string Operacion { get; }

    public AccesoDenegadoException(string operacion, string mensaje) : base(mensaje) => Operacion = operacion;
}

//una regla de aplicacion rechazo la operacion (validacion, recurso inexistente...)
public class ExcepcionAplicacion : Exception
{
    public string Codigo { get; }

    public ExcepcionAplicacion(string codigo, string mensaje) : base(mensaje) => Codigo = codigo;
}

//conflicto de persistencia traducido por Infrastructure (concurrencia optimista o restriccion unica)
//para que Application no dependa de EF Core
public sealed class ExcepcionConcurrencia : ExcepcionAplicacion
{
    public ExcepcionConcurrencia(string mensaje, Exception? interna = null) : base("CONFLICTO_CONCURRENCIA", mensaje)
    {
        if (interna is not null) Data["Interna"] = interna.Message;
    }
}

public sealed class ExcepcionCobroRechazado : ExcepcionAplicacion
{
    public ExcepcionCobroRechazado(string mensaje) : base("COBRO_RECHAZADO", mensaje) { }
}
