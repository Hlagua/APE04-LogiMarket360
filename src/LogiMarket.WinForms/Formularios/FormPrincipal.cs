using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using LogiMarket.WinForms.Vistas;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Formularios;

/// <summary>Ventana principal: navegación por pestañas hacia las 12 pantallas, coordinadas por el Mediator.</summary>
public sealed class FormPrincipal : Form
{
    private readonly Ejecutor _ejecutor;
    private readonly FormularioPrincipalMediator _mediador;
    private readonly TabControl _pestanas = new() { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
    private readonly Dictionary<string, TabPage> _paginas = new();
    private readonly ToolStripStatusLabel _estado = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly VistaCarrito _carrito;

    public bool CerroSesion { get; private set; }
    private bool _descarteConfirmado;

    public FormPrincipal(Ejecutor ejecutor, FormularioPrincipalMediator mediador, UsuarioSesion usuario)
    {
        _ejecutor = ejecutor;
        _mediador = mediador;
        Text = $"LogiMarket 360 - {usuario.NombreCompleto} ({usuario.Rol})";
        Font = Ui.FuenteNormal;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 650);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Ui.ColorPrimario, Padding = new Padding(8, 6, 8, 6) };
        cabecera.Controls.Add(new Label
        {
            Text = $"LogiMarket 360   |   Usuario: {usuario.NombreUsuario}   |   Rol: {usuario.Rol}",
            ForeColor = Color.White, AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), Margin = new Padding(3, 6, 20, 3)
        });
        var salir = Ui.Boton("Cerrar sesión", (_, _) => CerrarSesion());
        salir.BackColor = Color.White;
        cabecera.Controls.Add(salir);

        var barraEstado = new StatusStrip();
        barraEstado.Items.Add(_estado);
        // El Ejecutor es singleton y sobrevive a la sesión: hay que desuscribirse al cerrar.
        _ejecutor.Mensaje += MostrarEstado;
        FormClosed += (_, _) => _ejecutor.Mensaje -= MostrarEstado;

        _carrito = new VistaCarrito(ejecutor, mediador);
        Agregar("01·Panel", new VistaPanel(ejecutor, mediador));
        Agregar("02·Catálogo y kits", new VistaCatalogo(ejecutor, mediador));
        Agregar("03·Clientes", new VistaClientes(ejecutor, mediador));
        Agregar("04·Carrito", _carrito);
        Agregar("05·Pedidos", new VistaPedidos(ejecutor, mediador));
        Agregar("06·Cobro", new VistaCobro(ejecutor, mediador));
        Agregar("07·Despacho", new VistaDespacho(ejecutor, mediador));
        Agregar("08·Devoluciones", new VistaDevoluciones(ejecutor, mediador));
        Agregar("09·Inventario", new VistaInventario(ejecutor, mediador));
        Agregar("10·Reportes", new VistaReportes(ejecutor, mediador));
        Agregar("11·Auditoría y métricas", new VistaAuditoria(ejecutor, mediador));
        Agregar("12·Demostración", new VistaDemo(ejecutor, mediador));

        _mediador.MostrarVista = clave => { if (_paginas.TryGetValue(clave, out var p)) _pestanas.SelectedTab = p; };
        _pestanas.SelectedIndexChanged += async (_, _) =>
        {
            if (_pestanas.SelectedTab?.Controls[0] is IVistaColega v && v.Clave != FormularioPrincipalMediator.Carrito) await v.RefrescarAsync();
        };

        Controls.Add(_pestanas);
        Controls.Add(cabecera);
        Controls.Add(barraEstado);
        Shown += async (_, _) => await _mediador.NotificarAsync(null, EventoVista.SesionIniciada);
        FormClosing += (_, e) =>
        {
            if (!_descarteConfirmado && _carrito.TieneCambiosSinGuardar &&
                !Ui.Confirmar("El carrito tiene cambios sin guardar. ¿Salir de todos modos?", "Cambios sin guardar"))
                e.Cancel = true;
        };
        // Ctrl+1..9 para navegar entre pestañas con el teclado.
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode is >= Keys.D1 and <= Keys.D9) { _pestanas.SelectedIndex = e.KeyCode - Keys.D1; e.Handled = true; }
        };
    }

    private void MostrarEstado(string texto, bool ok)
    {
        _estado.Text = $"{DateTime.Now:HH:mm:ss}  {texto}";
        _estado.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
    }

    private void Agregar(string titulo, VistaBase vista)
    {
        var pagina = new TabPage(titulo) { BackColor = Ui.ColorFondo };
        pagina.Controls.Add(vista);
        _pestanas.TabPages.Add(pagina);
        _paginas[vista.Clave] = pagina;
        _mediador.Registrar(vista);
    }

    private async void CerrarSesion()
    {
        if (_carrito.TieneCambiosSinGuardar && !Ui.Confirmar("Hay cambios sin guardar en el carrito. ¿Cerrar sesión?")) return;
        _descarteConfirmado = true; // evita que FormClosing vuelva a preguntar lo mismo
        await _ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioAutenticacion>().CerrarSesionAsync());
        CerroSesion = true;
        Close();
    }
}
