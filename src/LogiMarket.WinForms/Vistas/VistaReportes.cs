using LogiMarket.Application.Reportes;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>11. Reportes y comprobantes: filtro (Interpreter), comprobante × medio (Bridge) y vista masiva (Flyweight).</summary>
public sealed class VistaReportes : VistaBase
{
    private readonly TextBox _filtro = Ui.Texto(380, "ESTADO = PAGADO AND TOTAL >= 50");
    private readonly Label _arbol = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 6, 3, 6) };
    private readonly DataGridView _resultado = Ui.Grilla(), _masiva = Ui.Grilla();
    private readonly ComboBox _tipo = Ui.Combo(120), _medio = Ui.Combo(120);
    private readonly NumericUpDown _pedido = Ui.Numero(1, 1_000_000, 0, 1);
    private readonly Label _medicion = new() { AutoSize = true, MaximumSize = new Size(1100, 0), Margin = new Padding(3, 6, 3, 6) };

    public VistaReportes(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Reportes)
    {
        _filtro.Text = "ESTADO = PAGADO AND TOTAL >= 50";
        _tipo.DataSource = Enum.GetValues<TipoComprobante>();
        _filtro.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FiltrarAsync(); } };
        _resultado.SelectionChanged += (_, _) =>
        {
            if (_resultado.CurrentRow?.DataBoundItem is Application.Consultas.PedidoResumenDto p) _pedido.Value = p.Id;
        };

        var pestanas = new TabControl();
        var filtro = new TabPage("Filtro de pedidos (Interpreter)");
        filtro.Controls.Add(Ui.Layout("Consulta con gramática ESTADO / TOTAL / CLIENTE, AND, OR y paréntesis",
            Ui.Barra(Ui.Etiqueta("Filtro"), _filtro, Ui.Boton("Aplicar", async (_, _) => await FiltrarAsync(), true)), _arbol, _resultado,
            Ui.Barra(Ui.Etiqueta("Comprobante"), _tipo, Ui.Etiqueta("Medio"), _medio, Ui.Etiqueta("Pedido N.º"), _pedido,
                Ui.Boton("Emitir comprobante (Bridge)", async (_, _) => await EmitirAsync()))));
        var masiva = new TabPage("Vista masiva (Flyweight)");
        masiva.Controls.Add(Ui.Layout("1.000 filas con metadatos de SKU compartidos",
            Ui.Barra(Ui.Boton("Generar y medir 1.000 filas", async (_, _) => await GenerarMasivaAsync(), true)), _medicion, _masiva));
        pestanas.TabPages.Add(filtro);
        pestanas.TabPages.Add(masiva);
        Ui.Ajustable(_medicion, this);
        Controls.Add(pestanas);
        pestanas.Dock = DockStyle.Fill;
    }

    public override Task RefrescarAsync()
    {
        using var scope = Ejecutor.Singleton<IServiceProvider>().CreateScope();
        var medios = scope.ServiceProvider.GetRequiredService<ServicioReportes>().MediosDisponibles.ToList();
        if (_medio.Items.Count != medios.Count) { _medio.DataSource = medios; }
        return Task.CompletedTask;
    }

    private async Task FiltrarAsync()
    {
        var (ok, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioReportes>().FiltrarPedidosAsync(_filtro.Text));
        Errores.SetError(_filtro, ok ? "" : "Revise la sintaxis del filtro");
        if (!ok || r is null) return;
        _arbol.Text = $"Árbol interpretado: {r.Arbol}  →  {r.Pedidos.Count} pedido(s)";
        Ui.Enlazar(_resultado, r.Pedidos);
    }

    private async Task EmitirAsync()
    {
        if (_medio.SelectedItem is not string medio) return;
        var tipo = (TipoComprobante)_tipo.SelectedItem!;
        var id = (int)_pedido.Value;
        var (ok, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioReportes>().EmitirComprobanteAsync(tipo, id, medio));
        if (ok && r is not null) Ejecutor.Estado($"Comprobante {tipo} emitido por {r.Medio}: {r.Destino} ({r.Caracteres} caracteres)");
        if (ok && r is not null && r.Medio != SalidaVistaPrevia.NombreMedio)
            MessageBox.Show($"Archivo generado:\n{r.Destino}", "Comprobante TXT", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task GenerarMasivaAsync()
    {
        var (ok, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioReportes>().VistaMasivaAsync(1000));
        if (!ok) return;
        var (filas, m) = r;
        Ui.Enlazar(_masiva, filas.Select((f, i) => new { Fila = i + 1, f.Sku, f.Nombre, f.Categoria, f.Stock, f.Cantidad, f.PrecioVenta,
            IdMetadatos = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(f.Metadatos) }));
        _medicion.Text = $"Filas: {m.Filas} | SKU distintos: {m.SkusDistintos} | Instancias de metadatos: {m.InstanciasMetadatos} | " +
                         $"ReferenceEquals por SKU: {m.ReferenciasCompartidas} | Memoria retenida con Flyweight ≈ {m.BytesConFlyweight:N0} B, " +
                         $"sin Flyweight ≈ {m.BytesSinFlyweight:N0} B (ahorro ≈ {m.AhorroPorcentual:0.0} %). Condiciones: {m.Condiciones}";
    }
}
