namespace LogiMarket.Application.Comun;

//reloj inyectable (permite simular dias transcurridos en pruebas de devolucion)
public interface IReloj
{
    DateTime Ahora { get; }
}

public sealed class RelojSistema : IReloj
{
    public DateTime Ahora => DateTime.Now;
}

public sealed record EntradaBitacora(DateTime Fecha, string Categoria, string Mensaje, string Correlacion)
{
    public override string ToString() => $"{Fecha:yyyy-MM-dd HH:mm:ss.fff} [{Categoria}] ({Correlacion}) {Mensaje}";
}

//bitacora tecnica (LOG)
//es independiente de la transaccion de BD: lo registrado aqui sobrevive a un rollback (necesario para
//observar INICIO -> PASARELA -> FIN/ERROR y denegaciones de seguridad)
public interface IBitacoraTecnica
{
    void Registrar(string categoria, string mensaje);
    IReadOnlyList<EntradaBitacora> Entradas(string? categoria = null);
}

//identificador de correlacion que fluye por llamadas asincronas (Decorator -> Adapter)
public static class ContextoCorrelacion
{
    private static readonly AsyncLocal<string?> Valor = new();

    public static string Actual => Valor.Value ?? "-";

    public static IDisposable Iniciar(string correlacion)
    {
        var anterior = Valor.Value;
        Valor.Value = correlacion;
        return new Restaurador(() => Valor.Value = anterior);
    }

    private sealed class Restaurador(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }
}

//opciones de la pantalla de DEMOSTRACION (solo ADMIN)
//permiten cambiar el proveedor simulado, inducir fallos y alternar el canal de aviso
//todo cambio queda en la bitacora
//por defecto estan desactivadas y no alteran las reglas de negocio ni los resultados reales
public sealed class ConfiguracionDemo
{
    public const string ProveedorLegado = "LEGADO";
    public const string ProveedorModerno = "MODERNO";
    public const string CanalEmail = "EMAIL";
    public const string CanalSms = "SMS";

    public string ProveedorPago { get; set; } = ProveedorLegado;
    public string CanalNotificacion { get; set; } = CanalEmail;
    public bool ForzarRechazoCobro { get; set; }
    public bool ForzarErrorPasarela { get; set; }
    public bool ForzarFalloNotificador { get; set; }

    public override string ToString() =>
        $"Proveedor={ProveedorPago}; Canal={CanalNotificacion}; RechazoCobro={ForzarRechazoCobro}; " +
        $"ErrorPasarela={ForzarErrorPasarela}; FalloNotificador={ForzarFalloNotificador}";
}
