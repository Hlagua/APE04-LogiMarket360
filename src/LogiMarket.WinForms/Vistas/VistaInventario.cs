using LogiMarket.Application.Administracion;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>10. Inventario y movimientos: existencias, alertas y kardex trazable.</summary>
public sealed class VistaInventario : VistaBase
{
    private readonly DataGridView _existencias = Ui.Grilla(), _movimientos = Ui.Grilla();
    private readonly NumericUpDown _nuevoStock = Ui.Numero(0, 100000);
    private readonly TextBox _motivo = Ui.Texto(220, "Motivo del ajuste");
    private readonly CheckBox _soloSeleccion = new() { Text = "Solo movimientos del SKU seleccionado", AutoSize = true, Margin = new Padding(10, 7, 3, 3) };
    private readonly Button _ajustar;

    public VistaInventario(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Inventario)
    {
        _existencias.SelectionChanged += async (_, _) => { if (_soloSeleccion.Checked) await CargarMovimientosAsync(); };
        _soloSeleccion.CheckedChanged += async (_, _) => await CargarMovimientosAsync();
        _existencias.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _existencias.Rows.Count) return;
            if (_existencias.Rows[e.RowIndex].DataBoundItem is ProductoDto p && p.Stock <= ServicioAdministracion.UmbralStockBajo)
                e.CellStyle!.BackColor = Color.FromArgb(255, 236, 236);
        };
        _ajustar = Ui.Boton("Ajustar stock (ADMIN)", async (_, _) => await AjustarAsync());
        var ajuste = Ui.Barra(Ui.Etiqueta("Nuevo stock"), _nuevoStock, Ui.Etiqueta("Motivo"), _motivo, _ajustar, _soloSeleccion);
        var split = Ui.Division(Orientation.Horizontal, 0.5);
        split.Panel1.Controls.Add(Ui.Layout($"Existencias (en rojo: stock ≤ {ServicioAdministracion.UmbralStockBajo})", _existencias, ajuste));
        split.Panel2.Controls.Add(Ui.Layout("Movimientos de inventario", _movimientos));
        Controls.Add(split);
    }

    public override async Task RefrescarAsync()
    {
        _ajustar.Enabled = Puede(Operacion.AjustarInventario);
        var (ok, productos) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().ListarProductosAsync());
        if (ok && productos is not null) Ui.Enlazar(_existencias, productos, nameof(ProductoDto.Sku));
        await CargarMovimientosAsync();
    }

    private async Task CargarMovimientosAsync()
    {
        var sku = _soloSeleccion.Checked ? Ui.Seleccionado<ProductoDto>(_existencias)?.Sku : null;
        var (ok, movs) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioAdministracion>().MovimientosAsync(sku));
        if (ok && movs is not null) Ui.Enlazar(_movimientos, movs);
    }

    private async Task AjustarAsync()
    {
        if (Ui.Seleccionado<ProductoDto>(_existencias) is not { } p) return;
        if (!Validar(_motivo, _motivo.Text.Trim().Length > 0, "Indique el motivo del ajuste")) return;
        if (!Ui.Confirmar($"¿Ajustar el stock de {p.Sku} de {p.Stock} a {_nuevoStock.Value}? Quedará registrado como movimiento AJUSTE.")) return;
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().AjustarStockAsync(p.Sku, (int)_nuevoStock.Value, _motivo.Text));
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.InventarioCambiado);
        await RefrescarAsync();
    }
}
