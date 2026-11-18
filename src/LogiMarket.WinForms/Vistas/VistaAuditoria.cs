using LogiMarket.Application.Administracion;
using LogiMarket.Application.Metricas;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>12a. Auditoría y métricas: auditoría persistente, bitácora técnica, Outbox, comandos y Mediator.</summary>
public sealed class VistaAuditoria : VistaBase
{
    private readonly FormularioPrincipalMediator _mediadorConcreto;
    private readonly ComboBox _categoria = Ui.Combo(130), _categoriaLog = Ui.Combo(130);
    private readonly TextBox _texto = Ui.Texto(180, "Texto a buscar");
    private readonly DataGridView _auditoria = Ui.Grilla(), _bitacora = Ui.Grilla(), _outbox = Ui.Grilla(), _comandos = Ui.Grilla();
    private readonly ListBox _trazasMediador = new() { IntegralHeight = false };
    private readonly Label _metricas = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };

    public VistaAuditoria(Ejecutor ejecutor, FormularioPrincipalMediator mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Auditoria)
    {
        _mediadorConcreto = mediador;
        _categoria.Items.AddRange(new object[] { "(todas)", "NEGOCIO", "RECHAZO", "SEGURIDAD", "EVENTO", "CATALOGO", "CLIENTES" });
        _categoriaLog.Items.AddRange(new object[] { "(todas)", "PAGO", "SEGURIDAD", "OUTBOX", "OBSERVER", "NOTIFICACION", "COMANDO", "DEMO", "REEMBOLSO", "SEMILLA" });
        _categoria.SelectedIndex = 0;
        _categoriaLog.SelectedIndex = 0;
        _categoria.SelectedIndexChanged += async (_, _) => await RefrescarAsync();
        _categoriaLog.SelectedIndexChanged += async (_, _) => await RefrescarAsync();
        _texto.TextChanged += async (_, _) => await RefrescarAsync();
        Ejecutor.Singleton<MetricasSesion>().Cambiaron += _ => { if (IsHandleCreated) BeginInvoke(MostrarMetricas); };

        var pestanas = new TabControl { Dock = DockStyle.Fill };
        Agregar(pestanas, "Auditoría (BD)", Ui.Layout("Auditoría de negocio y seguridad",
            Ui.Barra(Ui.Etiqueta("Categoría"), _categoria, Ui.Etiqueta("Buscar"), _texto), _auditoria));
        Agregar(pestanas, "Bitácora técnica (LOG)", Ui.Layout("LOG independiente de transacciones (INICIO → PASARELA → FIN/ERROR)",
            Ui.Barra(Ui.Etiqueta("Categoría"), _categoriaLog), _bitacora));
        Agregar(pestanas, "Outbox", Ui.Layout("Eventos pendientes y procesados", _outbox));
        Agregar(pestanas, "Comandos", Ui.Layout("Historial del invocador de comandos", _comandos));
        Agregar(pestanas, "Métricas y Mediator", Ui.Layout("Métricas de sesión (Singleton) y coordinación del Mediator", _metricas, _trazasMediador));
        Ui.Ajustable(_metricas, this);
        Controls.Add(Ui.Layout("Auditoría y métricas", Ui.Barra(Ui.Boton("Actualizar", async (_, _) => await RefrescarAsync())), pestanas));
    }

    private static void Agregar(TabControl t, string titulo, Control contenido)
    {
        var p = new TabPage(titulo);
        p.Controls.Add(contenido);
        t.TabPages.Add(p);
    }

    public override async Task RefrescarAsync()
    {
        string? Cat(ComboBox c) => c.SelectedIndex > 0 ? c.SelectedItem!.ToString() : null;
        var (ok, aud) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioAdministracion>().AuditoriaAsync(Cat(_categoria), _texto.Text));
        if (ok && aud is not null) Ui.Enlazar(_auditoria, aud);
        var (okO, outbox) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioAdministracion>().OutboxAsync());
        if (okO && outbox is not null) Ui.Enlazar(_outbox, outbox);
        await Ejecutor.Ejecutar(sp =>
        {
            var admin = sp.GetRequiredService<ServicioAdministracion>();
            Ui.Enlazar(_bitacora, admin.Bitacora(Cat(_categoriaLog)).Select(b => new { b.Fecha, b.Categoria, b.Correlacion, b.Mensaje }));
            Ui.Enlazar(_comandos, admin.HistorialComandos());
            return Task.CompletedTask;
        });
        _trazasMediador.DataSource = _mediadorConcreto.Trazas.Reverse().ToList();
        MostrarMetricas();
    }

    private void MostrarMetricas()
    {
        var m = Ejecutor.Singleton<MetricasSesion>().Instantanea();
        _metricas.Text = $"Instancia única {m.InstanciaId} (desde {m.Inicio:HH:mm:ss}) | Confirmados {m.PedidosConfirmados} | " +
                         $"Cobros rechazados {m.CobrosRechazados} | Repetidos {m.ConfirmacionesRepetidas} | Despachos {m.Despachos} | " +
                         $"Entregas {m.Entregas} | Devoluciones {m.Devoluciones} | Avisos {m.Notificaciones} | Eventos {m.EventosProcesados} | " +
                         $"Denegaciones {m.OperacionesDenegadas} | Cobrado {Ui.Usd(m.MontoCobrado)}";
    }
}
