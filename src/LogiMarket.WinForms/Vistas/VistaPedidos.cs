using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

/// <summary>06. Pedidos e historial: estados, trazabilidad, clonación (Prototype) y diagnóstico (Chain).</summary>
public sealed class VistaPedidos : VistaBase
{
    private readonly ComboBox _estado = Ui.Combo(130);
    private readonly TextBox _texto = Ui.Texto(160, "Cliente o número");
    private readonly DataGridView _pedidos = Ui.Grilla(), _lineas = Ui.Grilla(), _historial = Ui.Grilla();
    private readonly Label _resumen = new() { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    private IReadOnlyList<PedidoResumenDto> _todos = Array.Empty<PedidoResumenDto>();

    public VistaPedidos(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Pedidos)
    {
        _estado.Items.Add("(todos)");
        foreach (var e in Enum.GetNames<EstadoPedido>()) _estado.Items.Add(e);
        _estado.SelectedIndex = 0;
        _estado.SelectedIndexChanged += (_, _) => Filtrar();
        _texto.TextChanged += (_, _) => Filtrar();
        _pedidos.SelectionChanged += async (_, _) => await DetalleAsync();

        var filtros = Ui.Barra(Ui.Etiqueta("Estado"), _estado, Ui.Etiqueta("Buscar"), _texto, Ui.Boton("Actualizar", async (_, _) => await RefrescarAsync()));
        var acciones = Ui.Barra(
            Ui.Boton("Editar borrador", async (_, _) => await EnviarAsync(EventoVista.AbrirBorradorEnCarrito)),
            Ui.Boton("Duplicar como borrador", async (_, _) => await DuplicarAsync()),
            Ui.Boton("Diagnosticar validación", async (_, _) => await DiagnosticarAsync()),
            Ui.Boton("Cancelar pedido", async (_, _) => await CancelarAsync()),
            Ui.Boton("Cobrar", async (_, _) => await EnviarAsync(EventoVista.AbrirPedidoEnCobro), true),
            Ui.Boton("Despachar / entregar", async (_, _) => await EnviarAsync(EventoVista.AbrirPedidoEnDespacho)),
            Ui.Boton("Devolución", async (_, _) => await EnviarAsync(EventoVista.AbrirPedidoEnDevolucion)));

        var detalle = Ui.Division(Orientation.Vertical, 0.55);
        detalle.Panel1.Controls.Add(Ui.Layout("Líneas (instantánea de precios y kits)", _lineas));
        detalle.Panel2.Controls.Add(Ui.Layout("Historial de estados", _historial));
        var split = Ui.Division(Orientation.Horizontal, 0.5);
        split.Panel1.Controls.Add(Ui.Layout("Pedidos", filtros, _pedidos));
        split.Panel2.Controls.Add(Ui.Layout("Detalle", acciones, _resumen, detalle));
        Ui.Ajustable(_resumen, this);
        Controls.Add(split);
    }

    private int? Seleccionado => Ui.Seleccionado<PedidoResumenDto>(_pedidos)?.Id;

    public override async Task RefrescarAsync()
    {
        var (ok, lista) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().PedidosAsync());
        if (!ok || lista is null) return;
        _todos = lista;
        Filtrar();
    }

    private void Filtrar()
    {
        var estado = _estado.SelectedIndex > 0 ? _estado.SelectedItem!.ToString() : null;
        var t = _texto.Text.Trim();
        Ui.Enlazar(_pedidos, _todos.Where(p => (estado is null || p.Estado == estado) &&
            (t.Length == 0 || p.Cliente.Contains(t, StringComparison.OrdinalIgnoreCase) || p.Numero.Contains(t, StringComparison.OrdinalIgnoreCase))),
            nameof(PedidoResumenDto.Id));
    }

    private async Task DetalleAsync()
    {
        if (Seleccionado is not { } id) return;
        var (ok, d) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().PedidoAsync(id));
        if (!ok || d is null) return;
        Ui.Enlazar(_lineas, d.Lineas);
        Ui.Enlazar(_historial, d.Historial);
        var r = d.Resumen;
        _resumen.Text = $"{r.Numero} | {r.Cliente} ({r.Categoria}) | {r.Estado}{(r.Devuelto ? " (DEVUELTO)" : "")} | " +
                        $"Subtotal {Ui.Usd(r.Subtotal)} − {r.Politica} {Ui.Usd(r.Descuento)} + envío {Ui.Usd(r.Tarifa)} = {Ui.Usd(r.Total)}" +
                        (d.Pago is null ? "" : $" | Pago {d.Pago.Referencia} ({d.Pago.Proveedor})") +
                        (d.Despacho is null ? "" : $" | Guía {d.Despacho.NumeroGuia}") +
                        (d.Devolucion is null ? "" : $" | Reembolso {Ui.Usd(d.Devolucion.MontoReembolso)}");
    }

    private async Task EnviarAsync(EventoVista evento)
    {
        if (Seleccionado is { } id) await NotificarAsync(evento, id);
    }

    private async Task DuplicarAsync()
    {
        if (Seleccionado is not { } id) return;
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioPedidos>().ClonarComoBorradorAsync(id));
        if (Ejecutor.Mostrar(r, true)) await NotificarAsync(EventoVista.BorradorGuardado, r!.Valor);
        await RefrescarAsync();
    }

    private async Task DiagnosticarAsync()
    {
        if (Seleccionado is not { } id) return;
        var (ok, v) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioPedidos>().DiagnosticarAsync(id));
        if (ok && v is not null)
            MessageBox.Show($"{v}\n\nEslabones evaluados: {string.Join(" → ", v.Recorrido)}", "Cadena de validación",
                MessageBoxButtons.OK, v.EsValido ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private async Task CancelarAsync()
    {
        if (Seleccionado is not { } id) return;
        if (!Ui.Confirmar($"¿Cancelar definitivamente el pedido PED-{id:000000}? Esta acción no se puede revertir.")) return;
        var (_, r) = await Ejecutor.Ejecutar(async sp => await Ejecutor.Singleton<IInvocadorComandos>()
            .EjecutarAsync(new CancelarPedidoCommand(sp.GetRequiredService<IServicioPedidos>(), id, "Cancelado desde la pantalla de pedidos")));
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.PedidoCambiado, id);
        await RefrescarAsync();
    }
}
