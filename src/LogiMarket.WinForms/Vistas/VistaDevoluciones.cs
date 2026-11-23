using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

/// <summary>09. Devoluciones: política por motivo (Template Method), reintegro y reembolso único.</summary>
public sealed class VistaDevoluciones : VistaBase
{
    private readonly DataGridView _grilla = Ui.Grilla();
    private readonly ComboBox _motivo = Ui.Combo(170);
    private readonly CheckBox _sinUso = new() { Text = "Producto sin uso", AutoSize = true, Checked = true, Margin = new Padding(10, 7, 3, 3) };
    private readonly TextBox _observacion = Ui.Texto(260, "Observación");
    private readonly Label _estimado = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f), Margin = new Padding(3, 8, 3, 8) };
    private readonly TextBox _resultado = new() { Multiline = true, ReadOnly = true, Height = 110, Font = new Font("Consolas", 9.5f) };
    private readonly Button _procesar;

    public VistaDevoluciones(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Devoluciones)
    {
        _motivo.DataSource = Enum.GetValues<MotivoDevolucion>();
        _grilla.SelectionChanged += (_, _) => Estimar();
        _procesar = Ui.Boton("Procesar devolución total", async (_, _) => await ProcesarAsync(), true);
        var politica = new Label
        {
            AutoSize = true, ForeColor = Color.DimGray,
            Text = "DEFECTO: hasta 30 días desde la entrega. ARREPENTIMIENTO: hasta 7 días y sin uso. El reembolso excluye la tarifa de envío."
        };
        var fila = Ui.Barra(Ui.Etiqueta("Motivo"), _motivo, _sinUso, Ui.Etiqueta("Observación"), _observacion, _procesar);
        Controls.Add(Ui.Layout("Devoluciones (pedidos ENTREGADOS no devueltos)", politica, _grilla, _estimado, fila, _resultado));
    }

    public override async Task RefrescarAsync()
    {
        _procesar.Enabled = Puede(Operacion.ProcesarDevolucion);
        var (ok, lista) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().PedidosAsync());
        if (ok && lista is not null)
            Ui.Enlazar(_grilla, lista.Where(p => p.Estado == "ENTREGADO" && !p.Devuelto), nameof(PedidoResumenDto.Id));
        Estimar();
    }

    public override async Task RecibirPedidoAsync(int pedidoId)
    {
        await RefrescarAsync();
        foreach (DataGridViewRow fila in _grilla.Rows)
            if (fila.DataBoundItem is PedidoResumenDto p && p.Id == pedidoId) _grilla.CurrentCell = fila.Cells[0];
    }

    private void Estimar()
    {
        _estimado.Text = Ui.Seleccionado<PedidoResumenDto>(_grilla) is { } p
            ? $"{p.Numero}: pagado {Ui.Usd(p.Total)} − envío {Ui.Usd(p.Tarifa)} = reembolso posible {Ui.Usd(p.ImporteReembolsable)}" +
              (p.FechaEntrega is { } f ? $" | entregado hace {(DateTime.Now.Date - f.Date).Days} días" : "")
            : "Seleccione un pedido entregado.";
    }

    private async Task ProcesarAsync()
    {
        if (Ui.Seleccionado<PedidoResumenDto>(_grilla) is not { } p) return;
        var motivo = (MotivoDevolucion)_motivo.SelectedItem!;
        if (!Ui.Confirmar($"¿Procesar la devolución TOTAL de {p.Numero} por {motivo}? Si se acepta, se reintegra stock y se emite el reembolso (una sola vez).")) return;
        var solicitud = new SolicitudDevolucion(p.Id, motivo, _sinUso.Checked, _observacion.Text.Trim());
        var (_, r) = await Ejecutor.Ejecutar(async sp => await Ejecutor.Singleton<IInvocadorComandos>()
            .EjecutarAsync(new ProcesarDevolucionCommand(sp.GetRequiredService<IServicioDevoluciones>(), solicitud)));
        if (r is null) return;
        _resultado.Text = r is ResultadoDevolucion d
            ? $"{d}{Environment.NewLine}Pasos: {string.Join(" → ", d.Pasos)}{Environment.NewLine}Reembolso: {Ui.Usd(d.MontoReembolso)} ref {d.ReferenciaReembolso}"
            : r.ToString();
        if (Ejecutor.Mostrar(r, r.Exito)) await NotificarAsync(EventoVista.DevolucionRegistrada, p.Id);
        await RefrescarAsync();
    }
}
