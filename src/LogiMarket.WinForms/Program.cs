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

        var carpetaDatos = Path.Combine(AppContext.BaseDirectory, "datos");
        Directory.CreateDirectory(carpetaDatos);
        var opciones = new OpcionesLogiMarket
        {
            RutaBaseDatos = Environment.GetEnvironmentVariable("LOGIMARKET_DB") ?? Path.Combine(carpetaDatos, "logimarket.db"),
            CarpetaSalidas = Path.Combine(AppContext.BaseDirectory, "salidas"),
            CarpetaLogs = Path.Combine(AppContext.BaseDirectory, "logs")
        };

        var servicios = new ServiceCollection();
        servicios.AgregarLogiMarket(opciones);
        servicios.AddSingleton<Application.Puertos.ISalidaDocumento, SalidaVistaPrevia>(); // Bridge: medio visual
        servicios.AddSingleton<FormularioPrincipalMediator>();
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
