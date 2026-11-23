using LogiMarket.Application.Administracion;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Metricas;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

//02
//panel general: resumen de pedidos, existencias, alertas y metricas de sesion
public sealed class VistaPanel : VistaBase
{
    private readonly FlowLayoutPanel _tarjetas = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
    private readonly ListBox _alertas = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _metricas = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 8) };

    public VistaPanel(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Panel)
    {
        var alertas = new GroupBox { Text = "Alertas operativas", Dock = DockStyle.Fill, Padding = new Padding(8) };
        alertas.Controls.Add(_alertas);
        var barra = Ui.Barra(Ui.Boton("Actualizar", async (_, _) => await RefrescarAsync()));
        Ui.Ajustable(_metricas, this);
        Controls.Add(Ui.Layout("Panel general", barra, _tarjetas, _metricas, alertas));
        AlCambiarMetricas(MostrarMetricas);
    }

    public override async Task RefrescarAsync()
    {
        var (ok, ind) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().IndicadoresAsync());
        if (!ok || ind is null) return;
        _tarjetas.Controls.Clear();
        AgregarTarjeta("Borradores", ind.Borradores.ToString());
        AgregarTarjeta("Validados", ind.Validados.ToString());
        AgregarTarjeta("Pagados", ind.Pagados.ToString());
        AgregarTarjeta("Despachados", ind.Despachados.ToString());
        AgregarTarjeta("Entregados", ind.Entregados.ToString());
        AgregarTarjeta("Cancelados", ind.Cancelados.ToString());
        AgregarTarjeta("Devueltos", ind.Devueltos.ToString());
        AgregarTarjeta("Cobrado", Ui.Usd(ind.VentasCobradas));
        AgregarTarjeta("Reembolsado", Ui.Usd(ind.Reembolsado));
        AgregarTarjeta("Stock bajo", ind.ProductosStockBajo.ToString());
        AgregarTarjeta("Outbox pendiente", ind.OutboxPendientes.ToString());
        _alertas.DataSource = ind.Alertas.Count == 0 ? new List<string> { "Sin alertas." } : ind.Alertas.ToList();
        MostrarMetricas(Ejecutor.Singleton<IMetricasSesion>().Instantanea());
    }

    private void MostrarMetricas(InstantaneaMetricas m) =>
        _metricas.Text = $"Métricas de sesión (instancia {m.InstanciaId.ToString()[..8]}): confirmados {m.PedidosConfirmados}, " +
                         $"cobros rechazados {m.CobrosRechazados}, repetidos {m.ConfirmacionesRepetidas}, despachos {m.Despachos}, " +
                         $"entregas {m.Entregas}, devoluciones {m.Devoluciones}, avisos {m.Notificaciones}, denegaciones {m.OperacionesDenegadas}, " +
                         $"cobrado {Ui.Usd(m.MontoCobrado)}";

    private void AgregarTarjeta(string titulo, string valor)
    {
        var p = new Panel { Size = new Size(150, 70), BackColor = Color.White, Margin = new Padding(5), BorderStyle = BorderStyle.FixedSingle };
        p.Controls.Add(new Label { Text = valor, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Ui.ColorPrimario, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
        p.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Height = 22 });
        _tarjetas.Controls.Add(p);
    }
}
