using System.Globalization;
using LogiMarket.Application.Puertos;
using LogiMarket.Infrastructure;
using LogiMarket.WinForms.Formularios;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms;

/// <summary>
/// COMPOSITION ROOT. Único lugar donde WinForms toca Infrastructure: registrar dependencias y preparar la
/// base (migraciones + semilla idempotente). No contiene lógica de negocio.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        ConfigurarCultura();

        OpcionesLogiMarket opciones;
        try
        {
            opciones = LeerConfiguracion();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"appsettings.json no es válido:\n{ex.Message}", "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var servicios = new ServiceCollection();
        servicios.AgregarLogiMarket(opciones);
        servicios.AddSingleton<Application.Puertos.ISalidaDocumento, SalidaVistaPrevia>(); // Bridge: medio visual
        servicios.AddSingleton<Ejecutor>();
        using var proveedor = servicios.BuildServiceProvider();

        try
        {
            using var scope = proveedor.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInicializadorBaseDatos>().InicializarAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo preparar la base de datos:\n{ex.Message}", "LogiMarket 360", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var ejecutor = proveedor.GetRequiredService<Ejecutor>();
        while (true)
        {
            using var login = new FormLogin(ejecutor, opciones.RutaBaseDatos);
            if (login.ShowDialog() != DialogResult.OK || login.Usuario is null) return;

            // Mediador nuevo por sesión para no conservar vistas de la sesión anterior.
            var mediador = new FormularioPrincipalMediator();
            using var principal = new FormPrincipal(ejecutor, mediador, login.Usuario);
            System.Windows.Forms.Application.Run(principal);
            if (!principal.CerroSesion) return;
        }
    }

    /// <summary>
    /// Rutas de base, salidas y logs. Prioridad: variable LOGIMARKET_DB (solo la base) → appsettings.json
    /// junto al ejecutable (ver appsettings.ejemplo.json) → valores por defecto.
    /// </summary>
    private static OpcionesLogiMarket LeerConfiguracion()
    {
        var baseDir = AppContext.BaseDirectory;
        string Ruta(string? valor, string porDefecto) =>
            Path.GetFullPath(Path.Combine(baseDir, string.IsNullOrWhiteSpace(valor) ? porDefecto : valor));

        var archivo = Path.Combine(baseDir, "appsettings.json");
        var config = File.Exists(archivo)
            ? System.Text.Json.JsonSerializer.Deserialize<ConfiguracionArchivo>(File.ReadAllText(archivo),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip })
            : null;

        var rutaDb = Ruta(Environment.GetEnvironmentVariable("LOGIMARKET_DB") ?? config?.RutaBaseDatos, "datos/logimarket.db");
        Directory.CreateDirectory(Path.GetDirectoryName(rutaDb)!);
        return new OpcionesLogiMarket
        {
            RutaBaseDatos = rutaDb,
            CarpetaSalidas = Ruta(config?.CarpetaSalidas, "salidas"),
            CarpetaLogs = Ruta(config?.CarpetaLogs, "logs")
        };
    }

    private sealed record ConfiguracionArchivo(string? RutaBaseDatos, string? CarpetaSalidas, string? CarpetaLogs);

    /// <summary>Español (Ecuador) con punto decimal para que los importes coincidan con la guía.</summary>
    private static void ConfigurarCultura()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-EC").Clone();
        cultura.NumberFormat.NumberDecimalSeparator = ".";
        cultura.NumberFormat.NumberGroupSeparator = ",";
        cultura.NumberFormat.CurrencyDecimalSeparator = ".";
        cultura.NumberFormat.CurrencyGroupSeparator = ",";
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;
    }
}
