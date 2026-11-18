using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LogiMarket.WinForms.Infraestructura;

/// <summary>Componentes reutilizables y formato homogéneo de la interfaz (DRY).</summary>
public static class Ui
{
    public static readonly Font FuenteTitulo = new("Segoe UI Semibold", 13f);
    public static readonly Font FuenteNormal = new("Segoe UI", 9.5f);
    public static readonly Color ColorPrimario = Color.FromArgb(25, 90, 160);
    public static readonly Color ColorFondo = Color.FromArgb(246, 248, 251);

    public static Label Titulo(string texto) => new()
    {
        Text = texto, Font = FuenteTitulo, ForeColor = ColorPrimario, AutoSize = true, Margin = new Padding(3, 3, 3, 8)
    };

    public static Label Etiqueta(string texto, Control? para = null)
    {
        var l = new Label { Text = texto, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) };
        if (para is not null) l.TabIndex = Math.Max(0, para.TabIndex - 1);
        return l;
    }

    public static Button Boton(string texto, EventHandler alPulsar, bool primario = false)
    {
        var b = new Button
        {
            Text = texto, AutoSize = true, MinimumSize = new Size(90, 30), Margin = new Padding(3), UseVisualStyleBackColor = !primario
        };
        if (primario)
        {
            b.BackColor = ColorPrimario;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
        }
        b.Click += alPulsar;
        return b;
    }

    public static TextBox Texto(int ancho = 160, string? marca = null) => new() { Width = ancho, PlaceholderText = marca ?? "" };

    public static NumericUpDown Numero(decimal min, decimal max, int decimales = 0, decimal valor = 0) => new()
    {
        Minimum = min, Maximum = max, DecimalPlaces = decimales, Value = Math.Max(min, valor), Width = 90,
        ThousandsSeparator = false, Increment = decimales > 0 ? 0.01m : 1
    };

    public static ComboBox Combo(int ancho = 160) => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = ancho };

    public static FlowLayoutPanel Barra(params Control[] controles)
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 2, 0, 2) };
        p.Controls.AddRange(controles);
        return p;
    }

    /// <summary>DataGridView de solo lectura con formato decimal consistente y selección de fila completa.</summary>
    public static DataGridView Grilla()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            StandardTab = true // TAB sale de la grilla (navegación por teclado)
        };
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(225, 233, 245);
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        g.DataBindingComplete += (_, _) => FormatearColumnas(g);
        return g;
    }

    /// <summary>
    /// Enlaza datos conservando la fila seleccionada (por el valor de la columna clave) y el desplazamiento.
    /// </summary>
    public static void Enlazar<T>(DataGridView grilla, IEnumerable<T> datos, string? columnaClave = null)
    {
        object? claveSeleccionada = null;
        if (columnaClave is not null && grilla.CurrentRow?.DataBoundItem is { } actual)
            claveSeleccionada = actual.GetType().GetProperty(columnaClave)?.GetValue(actual);
        var primeraVisible = grilla.FirstDisplayedScrollingRowIndex;

        grilla.DataSource = new BindingList<T>(datos.ToList());

        if (claveSeleccionada is not null)
            foreach (DataGridViewRow fila in grilla.Rows)
                if (Equals(fila.DataBoundItem?.GetType().GetProperty(columnaClave!)?.GetValue(fila.DataBoundItem), claveSeleccionada))
                {
                    grilla.CurrentCell = fila.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
                    break;
                }
        if (primeraVisible >= 0 && primeraVisible < grilla.RowCount) grilla.FirstDisplayedScrollingRowIndex = primeraVisible;
    }

    public static T? Seleccionado<T>(DataGridView grilla) where T : class => grilla.CurrentRow?.DataBoundItem as T;

    private static void FormatearColumnas(DataGridView g)
    {
        foreach (DataGridViewColumn c in g.Columns)
        {
            c.HeaderText = Regex.Replace(c.DataPropertyName, "(?<=[a-z])([A-Z])", " $1");
            if (c.ValueType == typeof(decimal) || c.ValueType == typeof(decimal?))
            {
                c.DefaultCellStyle.Format = "0.00";
                c.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else if (c.ValueType == typeof(int)) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (c.ValueType == typeof(DateTime) || c.ValueType == typeof(DateTime?)) c.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            if (c.ValueType != null && !c.ValueType.IsValueType && c.ValueType != typeof(string)) c.Visible = false;
        }
    }

    /// <summary>
    /// Divisor que fija la proporción cuando ya tiene tamaño real (asignar SplitterDistance antes lanza
    /// excepción en WinForms) y se adapta al redimensionar la ventana.
    /// </summary>
    public static SplitContainer Division(Orientation orientacion, double proporcion)
    {
        var s = new SplitContainer { Orientation = orientacion, Dock = DockStyle.Fill };
        var aplicado = false;
        s.SizeChanged += (_, _) =>
        {
            var total = orientacion == Orientation.Horizontal ? s.Height : s.Width;
            if (aplicado || total < 200) return;
            s.SplitterDistance = (int)(total * proporcion);
            aplicado = true;
        };
        return s;
    }

    /// <summary>Hace que una etiqueta larga se ajuste al ancho de la vista en lugar de cortarse.</summary>
    public static Label Ajustable(Label etiqueta, Control vista)
    {
        etiqueta.AutoSize = true;
        vista.SizeChanged += (_, _) => etiqueta.MaximumSize = new Size(Math.Max(200, vista.ClientSize.Width - 40), 0);
        return etiqueta;
    }

    public static bool Confirmar(string mensaje, string titulo = "Confirmar acción") =>
        MessageBox.Show(mensaje, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public static string Usd(decimal valor) => "USD " + valor.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Contenedor estándar de una vista: título + barra + contenido que se ajusta al tamaño.</summary>
    public static TableLayoutPanel Layout(string titulo, params Control[] filas)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10), BackColor = ColorFondo };
        t.Controls.Add(Titulo(titulo));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (var f in filas)
        {
            f.Dock = DockStyle.Fill;
            t.Controls.Add(f);
            t.RowStyles.Add(f is DataGridView or SplitContainer or TabControl or TextBox { Multiline: true }
                ? new RowStyle(SizeType.Percent, 100)
                : new RowStyle(SizeType.AutoSize));
        }
        return t;
    }
}
