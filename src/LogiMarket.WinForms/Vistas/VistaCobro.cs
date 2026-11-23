using LogiMarket.Application.Administracion;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

/// <summary>07. Confirmación y cobro: ejecuta ConfirmarPedidoCommand (Facade) mediante el invocador.</summary>
public sealed class VistaCobro : VistaBase
{
    private readonly ComboBox _pedido = Ui.Combo(320), _proveedor = Ui.Combo(120);
    private readonly TextBox _clave = Ui.Texto(160);
    private readonly Label _importes = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), Margin = new Padding(3, 10, 3, 10) };
    private readonly TextBox _resultado = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f) };
    private readonly Button _confirmar;

    public VistaCobro(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Cobro)
    {
        _proveedor.Items.AddRange(new object[] { ConfiguracionDemo.ProveedorLegado, ConfiguracionDemo.ProveedorModerno });
        _proveedor.SelectedIndexChanged += (_, _) => CambiarProveedor();
        _pedido.SelectedIndexChanged += async (_, _) => await MostrarImportesAsync();
        _confirmar = Ui.Boton("Confirmar y cobrar", async (_, _) => await ConfirmarAsync(), true);
        var fila1 = Ui.Barra(Ui.Etiqueta("Pedido"), _pedido, Ui.Etiqueta("Proveedor simulado"), _proveedor);
        var fila2 = Ui.Barra(Ui.Etiqueta("Clave de idempotencia"), _clave, _confirmar);
        var nota = new Label
        {
            AutoSize = true, ForeColor = Color.DimGray,
            Text = "El cobro pasa por PagoAutorizadoProxy → AuditoriaPagoDecorator → proveedor (Adapter). Repetir con la misma clave devuelve el resultado original sin cobrar dos veces."
        };
        Controls.Add(Ui.Layout("Confirmación y cobro", fila1, _importes, fila2, nota, _resultado));
    }

    public override async Task RefrescarAsync()
    {
        var seleccionado = _pedido.SelectedValue;
        var (ok, lista) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().PedidosAsync());
        if (ok && lista is not null)
        {
            _pedido.DisplayMember = "Texto";
            _pedido.ValueMember = "Id";
            // Se incluyen los PAGADOS para poder demostrar P07: repetir con la misma clave devuelve el
            // resultado original sin un segundo cobro (lo garantizan la idempotencia y el State).
            _pedido.DataSource = lista.Where(p => p.Estado is "BORRADOR" or "VALIDADO" or "PAGADO")
                .Select(p => new { p.Id, Texto = $"{p.Numero} - {p.Cliente} - {p.Estado} - {Ui.Usd(p.Total)}" }).ToList();
            if (seleccionado is not null) _pedido.SelectedValue = seleccionado;
        }
        _proveedor.Enabled = Puede(Operacion.AdministrarDemo);
        _proveedor.SelectedItem = Ejecutor.Singleton<ConfiguracionDemo>().ProveedorPago;
        _confirmar.Enabled = Puede(Operacion.ConfirmarPedido);
        await MostrarImportesAsync();
    }

    public override async Task RecibirPedidoAsync(int pedidoId)
    {
        await RefrescarAsync();
        _pedido.SelectedValue = pedidoId;
        await MostrarImportesAsync();
    }

    private int? PedidoId => _pedido.SelectedValue as int?;

    private async Task MostrarImportesAsync()
    {
        if (PedidoId is not { } id) { _importes.Text = "No hay pedidos en BORRADOR, VALIDADO o PAGADO."; return; }
        var (ok, d) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().PedidoAsync(id));
        if (!ok || d is null) return;
        _clave.Text = ConfirmarPedidoCommand.ClavePorDefecto(id, d.Resumen.FechaCreacion);
        var r = d.Resumen;
        _importes.Text = $"Subtotal {Ui.Usd(r.Subtotal)}  −  Descuento {r.Politica} {Ui.Usd(r.Descuento)}  +  Envío {r.Entrega} {Ui.Usd(r.Tarifa)}  =  TOTAL {Ui.Usd(r.Total)}";
    }

    private void CambiarProveedor()
    {
        if (!_proveedor.Enabled || _proveedor.SelectedItem is not string p || p == Ejecutor.Singleton<ConfiguracionDemo>().ProveedorPago) return;
        using var scope = Ejecutor.Singleton<IServiceProvider>().CreateScope();
        scope.ServiceProvider.GetRequiredService<IServicioAdministracion>().AplicarDemo(c => c.ProveedorPago = p);
        Ejecutor.Estado($"Proveedor de cobro simulado: {p}");
    }

    private async Task ConfirmarAsync()
    {
        if (PedidoId is not { } id || !Validar(_clave, _clave.Text.Trim().Length > 0, "La clave es obligatoria")) return;
        if (!Ui.Confirmar($"Se cobrará el pedido PED-{id:000000} y se descontará el stock. ¿Continuar?", "Confirmar cobro")) return;
        _confirmar.Enabled = false;
        try
        {
            var (_, r) = await Ejecutor.Ejecutar(async sp => await Ejecutor.Singleton<IInvocadorComandos>()
                .EjecutarAsync(new ConfirmarPedidoCommand(sp.GetRequiredService<IConfirmacionPedido>(), id, _clave.Text.Trim())));
            if (r is null) return;
            _resultado.Text = $"{r}{Environment.NewLine}{Environment.NewLine}LOG de cobro:{Environment.NewLine}" +
                string.Join(Environment.NewLine, Ejecutor.Singleton<IBitacoraTecnica>().Entradas("PAGO").TakeLast(4));
            Ejecutor.Mostrar(r, r.Exito);
            if (r.Exito) await NotificarAsync(EventoVista.PedidoConfirmado, id);
            else await NotificarAsync(EventoVista.MetricasCambiadas, id);
            await RefrescarAsync(); // el emisor no lo refresca el Mediator: se actualiza el estado mostrado
        }
        finally
        {
            _confirmar.Enabled = Puede(Operacion.ConfirmarPedido);
        }
    }
}
