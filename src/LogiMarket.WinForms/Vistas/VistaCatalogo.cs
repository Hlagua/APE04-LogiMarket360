using System.Globalization;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>03. Catálogo y kits: CRUD de productos, kits anidados (Composite) y desglose (Iterator/Visitor).</summary>
public sealed class VistaCatalogo : VistaBase
{
    private readonly TextBox _buscar = Ui.Texto(200, "Buscar SKU, nombre o categoría");
    private readonly DataGridView _productos = Ui.Grilla();
    private readonly DataGridView _kits = Ui.Grilla();
    private readonly TextBox _sku = Ui.Texto(90), _nombre = Ui.Texto(180), _categoria = Ui.Texto(120), _descripcion = Ui.Texto(200);
    private readonly NumericUpDown _precio = Ui.Numero(0, 100000, 2), _peso = Ui.Numero(0, 1000, 3), _stock = Ui.Numero(0, 100000);
    private readonly TextBox _kitCodigo = Ui.Texto(130, "KIT_NUEVO"), _kitNombre = Ui.Texto(160, "Nombre del kit"),
        _kitComponentes = Ui.Texto(220, "P01x1, KIT_OFICINA x1");
    private readonly TextBox _detalleKit = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f), Height = 120 };
    private readonly List<Control> _soloAdmin = new();

    public VistaCatalogo(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Catalogo)
    {
        _precio.DecimalPlaces = 2;
        _peso.DecimalPlaces = 3;
        _peso.Increment = 0.01m;
        _buscar.TextChanged += async (_, _) => await RefrescarAsync();
        _productos.SelectionChanged += (_, _) => CargarProducto();
        _kits.SelectionChanged += async (_, _) => await MostrarDesgloseAsync();

        var editor = Ui.Barra(Ui.Etiqueta("SKU"), _sku, Ui.Etiqueta("Nombre"), _nombre, Ui.Etiqueta("Categoría"), _categoria,
            Ui.Etiqueta("Descripción"), _descripcion, Ui.Etiqueta("Precio USD"), _precio, Ui.Etiqueta("Peso kg"), _peso,
            Ui.Etiqueta("Stock inicial"), _stock);
        var botones = Ui.Barra(
            Admin(Ui.Boton("Nuevo", (_, _) => Limpiar())),
            Admin(Ui.Boton("Crear producto", async (_, _) => await GuardarAsync(true), true)),
            Admin(Ui.Boton("Guardar cambios", async (_, _) => await GuardarAsync(false))),
            Admin(Ui.Boton("Activar / desactivar", async (_, _) => await AlternarActivoAsync())));

        var kitsEditor = Ui.Barra(Ui.Etiqueta("Código"), _kitCodigo, Ui.Etiqueta("Nombre"), _kitNombre,
            Ui.Etiqueta("Componentes"), _kitComponentes,
            Admin(Ui.Boton("Crear kit", async (_, _) => await CrearKitAsync())),
            Admin(Ui.Boton("Agregar componentes al kit seleccionado", async (_, _) => await AgregarComponentesAsync())),
            Admin(Ui.Boton("Quitar componente", async (_, _) => await QuitarComponenteAsync())));

        var split = Ui.Division(Orientation.Horizontal, 0.62);
        split.Panel1.Controls.Add(Ui.Layout("Productos", Ui.Barra(Ui.Etiqueta("Buscar"), _buscar), _productos, editor, botones));
        split.Panel2.Controls.Add(Ui.Layout("Kits (productos u otros kits, sin ciclos)", _kits, kitsEditor, _detalleKit));
        Controls.Add(split);
    }

    private Control Admin(Control c)
    {
        _soloAdmin.Add(c);
        return c;
    }

    public override async Task RefrescarAsync()
    {
        _soloAdmin.ForEach(c => c.Enabled = Puede(Operacion.GestionarCatalogo));
        var (ok, productos) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().ListarProductosAsync(_buscar.Text));
        if (ok && productos is not null) Ui.Enlazar(_productos, productos, nameof(ProductoDto.Sku));
        var (okK, kits) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().ListarKitsAsync());
        if (okK && kits is not null)
            Ui.Enlazar(_kits, kits.Select(k => new { k.Codigo, k.Nombre, Precio = k.Precio, PesoKg = k.PesoKg, StockVendible = k.StockDisponible, k.Composicion }), "Codigo");
    }

    private void CargarProducto()
    {
        if (Ui.Seleccionado<ProductoDto>(_productos) is not { } p) return;
        _sku.Text = p.Sku;
        _sku.ReadOnly = true;
        _nombre.Text = p.Nombre;
        _categoria.Text = p.Categoria;
        _descripcion.Text = p.Descripcion;
        _precio.Value = p.Precio;
        _peso.Value = p.PesoKg;
        _stock.Value = p.Stock;
        _stock.Enabled = false; // el stock se cambia en Inventario con movimiento trazable
    }

    private void Limpiar()
    {
        foreach (var t in new[] { _sku, _nombre, _categoria, _descripcion }) t.Clear();
        _sku.ReadOnly = false;
        _stock.Enabled = true;
        _precio.Value = 0;
        _peso.Value = 0;
        _stock.Value = 0;
        _sku.Focus();
    }

    private async Task GuardarAsync(bool nuevo)
    {
        if (!Validar(_sku, _sku.Text.Trim().Length > 0, "SKU obligatorio") ||
            !Validar(_nombre, _nombre.Text.Trim().Length > 0, "Nombre obligatorio") ||
            !Validar(_categoria, _categoria.Text.Trim().Length > 0, "Categoría obligatoria") ||
            !Validar(_precio, _precio.Value > 0, "El precio debe ser mayor que cero")) return;
        var datos = new ProductoEdicion(_sku.Text, _nombre.Text, _categoria.Text, _descripcion.Text, _precio.Value, _peso.Value, (int)_stock.Value);
        var (_, r) = await Ejecutor.Ejecutar(sp => nuevo
            ? sp.GetRequiredService<ServicioCatalogo>().CrearProductoAsync(datos)
            : sp.GetRequiredService<ServicioCatalogo>().ActualizarProductoAsync(datos));
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.CatalogoCambiado);
    }

    private async Task AlternarActivoAsync()
    {
        if (Ui.Seleccionado<ProductoDto>(_productos) is not { } p) return;
        if (!Ui.Confirmar($"¿{(p.Activo ? "Desactivar" : "Activar")} el producto {p.Sku}?")) return;
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().CambiarEstadoProductoAsync(p.Sku, !p.Activo));
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.CatalogoCambiado);
    }

    /// <summary>Interpreta "P01x1, KIT_OFICINA x2" como lista de componentes.</summary>
    private bool LeerComponentes(out List<(string, int)> componentes)
    {
        componentes = new List<(string, int)>();
        foreach (var parte in _kitComponentes.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trozos = parte.Replace(" ", "").Split('x', 'X');
            var codigo = string.Join("x", trozos[..^1]);
            if (trozos.Length < 2 || codigo.Length == 0 || !int.TryParse(trozos[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
                return Validar(_kitComponentes, false, $"Formato inválido en '{parte}'. Use CODIGOxCANTIDAD separados por coma.");
            componentes.Add((codigo.ToUpperInvariant(), n));
        }
        return Validar(_kitComponentes, componentes.Count > 0, "Indique al menos un componente.");
    }

    private async Task CrearKitAsync()
    {
        if (!Validar(_kitCodigo, _kitCodigo.Text.Trim().Length > 0, "Código obligatorio") ||
            !Validar(_kitNombre, _kitNombre.Text.Trim().Length > 0, "Nombre obligatorio") || !LeerComponentes(out var comps)) return;
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().CrearKitAsync(_kitCodigo.Text, _kitNombre.Text, comps));
        if (Ejecutor.Mostrar(r)) await NotificarAsync(EventoVista.CatalogoCambiado);
    }

    private string? KitSeleccionado() => _kits.CurrentRow?.Cells["Codigo"].Value as string;

    private async Task AgregarComponentesAsync()
    {
        if (KitSeleccionado() is not { } kit || !LeerComponentes(out var comps)) return;
        foreach (var (codigo, cantidad) in comps)
        {
            var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().AgregarComponenteAsync(kit, codigo, cantidad));
            if (!Ejecutor.Mostrar(r)) break;
        }
        await NotificarAsync(EventoVista.CatalogoCambiado);
        await RefrescarAsync();
    }

    private async Task QuitarComponenteAsync()
    {
        if (KitSeleccionado() is not { } kit || !LeerComponentes(out var comps)) return;
        if (!Ui.Confirmar($"¿Quitar {string.Join(", ", comps.Select(c => c.Item1))} del kit {kit}?")) return;
        foreach (var (codigo, _) in comps)
        {
            var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().QuitarComponenteAsync(kit, codigo));
            if (!Ejecutor.Mostrar(r)) break;
        }
        await NotificarAsync(EventoVista.CatalogoCambiado);
        await RefrescarAsync();
    }

    private async Task MostrarDesgloseAsync()
    {
        if (KitSeleccionado() is not { } kit) return;
        var (ok, d) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioCatalogo>().DesglosarAsync(kit));
        if (!ok || d is null) return;
        _detalleKit.Text =
            $"{d.Codigo}: precio {Ui.Usd(d.Precio)} | {d.ResumenPeso}{Environment.NewLine}" +
            $"Iterator (SKU finales): {string.Join(", ", d.ComponentesIterador.Select(c => $"{c.Sku}x{c.Cantidad}"))}{Environment.NewLine}" +
            $"Visitor (kits recorridos): {string.Join("  ", d.KitsVisitados)}{Environment.NewLine}" +
            $"Visitor (desglose): {string.Join(", ", d.LineasVisitor.Select(l => $"{new string('·', l.Nivel)}{l.Sku}x{l.Cantidad}"))}";
    }
}
