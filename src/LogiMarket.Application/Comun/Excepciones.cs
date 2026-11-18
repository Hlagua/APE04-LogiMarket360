namespace LogiMarket.Application.Comun;

/// <summary>El usuario de la sesión no tiene permiso para la operación (política en Application).</summary>
public sealed class AccesoDenegadoException : Exception
{
    public string Operacion { get; }

    public AccesoDenegadoException(string operacion, string mensaje) : base(mensaje) => Operacion = operacion;
}

/// <summary>Una regla de aplicación rechazó la operación (validación, recurso inexistente...).</summary>
public class ExcepcionAplicacion : Exception
{
    public string Codigo { get; }

    public ExcepcionAplicacion(string codigo, string mensaje) : base(mensaje) => Codigo = codigo;
}

/// <summary>
/// Conflicto de persistencia traducido por Infrastructure (concurrencia optimista o restricción única),
/// para que Application no dependa de EF Core.
/// </summary>
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
