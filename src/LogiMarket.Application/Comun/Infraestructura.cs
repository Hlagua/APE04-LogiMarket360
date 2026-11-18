namespace LogiMarket.Application.Comun;

/// <summary>Reloj inyectable (permite simular días transcurridos en pruebas de devolución).</summary>
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

/// <summary>
/// Bitácora técnica (LOG). Es independiente de la transacción de BD: lo registrado aquí sobrevive a un
/// rollback (necesario para observar INICIO → PASARELA → FIN/ERROR y denegaciones de seguridad).
/// </summary>
public interface IBitacoraTecnica
{
    void Registrar(string categoria, string mensaje);
    IReadOnlyList<EntradaBitacora> Entradas(string? categoria = null);
}

/// <summary>Identificador de correlación que fluye por llamadas asíncronas (Decorator → Adapter).</summary>
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

/// <summary>
/// Opciones de la pantalla de DEMOSTRACIÓN (solo ADMIN). Permiten cambiar el proveedor simulado,
/// inducir fallos y alternar el canal de aviso. Todo cambio queda en la bitácora; por defecto están
/// desactivadas y no alteran las reglas de negocio ni los resultados reales.
/// </summary>
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
