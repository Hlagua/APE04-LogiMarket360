using LogiMarket.Application.Comandos;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Logistica;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>08. Despacho y entrega (BODEGA/ADMIN): guía de la familia de entrega y seguimiento.</summary>
public sealed class VistaDespacho : VistaBase
{
    private readonly DataGridView _grilla = Ui.Grilla();
    private readonly TextBox _seguimiento = Ui.Texto(200, "Guía o número de pedido");
    private readonly TextBox _resultado = new() { Multiline = true, ReadOnly = true, Height = 90, Font = new Font("Consolas", 9.5f) };
    private readonly CheckBox _todos = new() { Text = "Mostrar todos los estados", AutoSize = true, Margin = new Padding(10, 7, 3, 3) };
    private readonly Button _despachar, _entregar;

    public VistaDespacho(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Despacho)
    {
        _todos.CheckedChanged += async (_, _) => await RefrescarAsync();
        _despachar = Ui.Boton("Despachar", async (_, _) => await EjecutarAsync(true), true);
        _entregar = Ui.Boton("Registrar entrega", async (_, _) => await EjecutarAsync(false));
        var acciones = Ui.Barra(_despachar, _entregar, _todos);
        var seguimiento = Ui.Barra(Ui.Etiqueta("Seguimiento"), _seguimiento, Ui.Boton("Consultar", async (_, _) => await SeguirAsync()));
        Controls.Add(Ui.Layout("Despacho y entrega", acciones, _grilla, seguimiento, _resultado));
    }

    public override async Task RefrescarAsync()
    {
        _despachar.Enabled = Puede(Operacion.Despachar);
        _entregar.Enabled = Puede(Operacion.Entregar);
        var (ok, lista) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioPedidos>().ListarAsync());
        if (ok && lista is not null)
            Ui.Enlazar(_grilla, lista.Where(p => _todos.Checked || p.Estado is "PAGADO" or "DESPACHADO"), nameof(PedidoResumenDto.Id));
    }

    public override async Task RecibirPedidoAsync(int pedidoId)
    {
        _todos.Checked = true;
        await RefrescarAsync();
        foreach (DataGridViewRow fila in _grilla.Rows)
            if (fila.DataBoundItem is PedidoResumenDto p && p.Id == pedidoId) _grilla.CurrentCell = fila.Cells[0];
    }

    private async Task EjecutarAsync(bool despachar)
    {
        if (Ui.Seleccionado<PedidoResumenDto>(_grilla) is not { } p) return;
        if (!Ui.Confirmar($"¿{(despachar ? "Despachar" : "Registrar la entrega de")} {p.Numero}? El cambio de estado no se puede revertir.")) return;
        var (_, r) = await Ejecutor.Ejecutar(async sp =>
        {
            var logistica = sp.GetRequiredService<ServicioLogistica>();
            IComando comando = despachar ? new DespacharPedidoCommand(logistica, p.Id) : new EntregarPedidoCommand(logistica, p.Id);
            return await Ejecutor.Singleton<InvocadorComandos>().EjecutarAsync(comando);
        });
        if (r is null) return;
        _resultado.Text = r.ToString();
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.PedidoCambiado, p.Id);
        await RefrescarAsync();
    }

    private async Task SeguirAsync()
    {
        var texto = _seguimiento.Text.Trim();
        if (texto.Length == 0 && Ui.Seleccionado<PedidoResumenDto>(_grilla) is { } p) texto = p.Id.ToString();
        if (!Validar(_seguimiento, texto.Length > 0, "Indique guía o pedido")) return;
        var (ok, s) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioLogistica>().SeguimientoAsync(texto));
        if (ok) _resultado.Text = s;
    }
}
