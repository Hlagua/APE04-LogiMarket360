using LogiMarket.Application.Administracion;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

//12b
//demostracion (solo ADMIN):
//proveedor simulado, fallos inducidos, Outbox y restablecer base de pruebas
public sealed class VistaDemo : VistaBase
{
    private readonly ComboBox _proveedor = Ui.Combo(120), _canal = Ui.Combo(100);
    private readonly CheckBox _rechazo = Check("Forzar rechazo de cobro"), _error = Check("Forzar error (timeout) de pasarela"),
        _falloAviso = Check("Forzar fallo del notificador (Outbox)");
    private readonly TextBox _salida = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f) };
    private readonly List<Control> _controles = new();

    public VistaDemo(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Demo)
    {
        _proveedor.Items.AddRange(new object[] { ConfiguracionDemo.ProveedorLegado, ConfiguracionDemo.ProveedorModerno });
        _canal.Items.AddRange(new object[] { ConfiguracionDemo.CanalEmail, ConfiguracionDemo.CanalSms });
        var aviso = new Label
        {
            AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.FromArgb(150, 80, 0),
            Text = "Opciones exclusivas de demostración: solo cambian colaboradores SIMULADOS (proveedor, canal, fallos inducidos). " +
                   "No modifican reglas, importes ni resultados; cada cambio queda en la bitácora (categoría DEMO)."
        };
        var config = Ui.Barra(Ui.Etiqueta("Proveedor de cobro"), C(_proveedor), Ui.Etiqueta("Canal de aviso"), C(_canal), C(_rechazo), C(_error), C(_falloAviso),
            C(Ui.Boton("Aplicar configuración", async (_, _) => await AplicarAsync(), true)));
        var acciones = Ui.Barra(
            C(Ui.Boton("Procesar Outbox pendiente", async (_, _) => await ProcesarOutboxAsync())),
            C(Ui.Boton("Restablecer base de pruebas", async (_, _) => await RestablecerAsync())));
        Controls.Add(Ui.Layout("Demostración y escenarios controlados", aviso, config, acciones, _salida));
    }

    private static CheckBox Check(string texto) => new() { Text = texto, AutoSize = true, Margin = new Padding(10, 7, 3, 3) };

    private Control C(Control c)
    {
        _controles.Add(c);
        return c;
    }

    public override Task RefrescarAsync()
    {
        var admin = Puede(Operacion.AdministrarDemo);
        _controles.ForEach(c => c.Enabled = admin);
        var demo = Ejecutor.Singleton<ConfiguracionDemo>();
        _proveedor.SelectedItem = demo.ProveedorPago;
        _canal.SelectedItem = demo.CanalNotificacion;
        _rechazo.Checked = demo.ForzarRechazoCobro;
        _error.Checked = demo.ForzarErrorPasarela;
        _falloAviso.Checked = demo.ForzarFalloNotificador;
        if (!admin) _salida.Text = "Pantalla disponible solo para el rol ADMIN.";
        return Task.CompletedTask;
    }

    private async Task AplicarAsync()
    {
        await Ejecutor.Ejecutar(sp =>
        {
            sp.GetRequiredService<IServicioAdministracion>().AplicarDemo(c =>
            {
                c.ProveedorPago = (string)_proveedor.SelectedItem!;
                c.CanalNotificacion = (string)_canal.SelectedItem!;
                c.ForzarRechazoCobro = _rechazo.Checked;
                c.ForzarErrorPasarela = _error.Checked;
                c.ForzarFalloNotificador = _falloAviso.Checked;
            });
            _salida.Text = $"Configuración aplicada: {Ejecutor.Singleton<ConfiguracionDemo>()}";
            return Task.CompletedTask;
        });
    }

    private async Task ProcesarOutboxAsync()
    {
        var (ok, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioAdministracion>().ProcesarOutboxAsync());
        if (!ok || r is null) return;
        _salida.Text = $"Procesados: {r.Procesados} | Fallidos: {r.Fallidos}{Environment.NewLine}{string.Join(Environment.NewLine, r.Detalle)}";
        await NotificarAsync(EventoVista.MetricasCambiadas);
    }

    private async Task RestablecerAsync()
    {
        if (!Ui.Confirmar("Se BORRARÁ la base de pruebas y se volverá a crear con los datos controlados de la guía " +
                          "(5 productos, 2 kits, 4 usuarios, 3 clientes). ¿Continuar?", "Restablecer base de pruebas")) return;
        if (await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioAdministracion>().RestablecerBaseAsync()))
        {
            _salida.Text = "Base restablecida a los datos controlados.";
            await NotificarAsync(EventoVista.BaseRestablecida);
        }
    }
}
