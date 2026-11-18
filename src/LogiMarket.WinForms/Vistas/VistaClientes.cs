using LogiMarket.Application.Clientes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Clientes;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Vistas;

/// <summary>04. Clientes: búsqueda, registro, categoría y edición.</summary>
public sealed class VistaClientes : VistaBase
{
    private readonly TextBox _buscar = Ui.Texto(220, "Buscar nombre, identificación o email");
    private readonly DataGridView _grilla = Ui.Grilla();
    private readonly TextBox _identificacion = Ui.Texto(110), _nombre = Ui.Texto(180), _email = Ui.Texto(180), _telefono = Ui.Texto(110);
    private readonly ComboBox _categoria = Ui.Combo(120);
    private readonly List<Control> _edicion = new();
    private int _idActual;

    public VistaClientes(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Clientes)
    {
        _categoria.DataSource = Enum.GetValues<CategoriaCliente>();
        _buscar.TextChanged += async (_, _) => await RefrescarAsync();
        _grilla.SelectionChanged += (_, _) => Cargar();
        var campos = Ui.Barra(Ui.Etiqueta("Identificación"), _identificacion, Ui.Etiqueta("Nombre"), _nombre, Ui.Etiqueta("Email"), _email,
            Ui.Etiqueta("Teléfono"), _telefono, Ui.Etiqueta("Categoría"), _categoria);
        var botones = Ui.Barra(
            Ed(Ui.Boton("Nuevo", (_, _) => Limpiar())),
            Ed(Ui.Boton("Guardar", async (_, _) => await GuardarAsync(), true)),
            Ed(Ui.Boton("Activar / desactivar", async (_, _) => await AlternarAsync())));
        Controls.Add(Ui.Layout("Clientes", Ui.Barra(Ui.Etiqueta("Buscar"), _buscar), _grilla, campos, botones));
    }

    private Control Ed(Control c)
    {
        _edicion.Add(c);
        return c;
    }

    public override async Task RefrescarAsync()
    {
        _edicion.ForEach(c => c.Enabled = Puede(Operacion.GestionarClientes));
        var (ok, lista) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioClientes>().BuscarAsync(_buscar.Text));
        if (ok && lista is not null) Ui.Enlazar(_grilla, lista, nameof(ClienteDto.Id));
    }

    private void Cargar()
    {
        if (Ui.Seleccionado<ClienteDto>(_grilla) is not { } c) return;
        _idActual = c.Id;
        _identificacion.Text = c.Identificacion;
        _identificacion.ReadOnly = true;
        _nombre.Text = c.Nombre;
        _email.Text = c.Email;
        _telefono.Text = c.Telefono;
        _categoria.SelectedItem = Enum.Parse<CategoriaCliente>(c.Categoria);
    }

    private void Limpiar()
    {
        _idActual = 0;
        foreach (var t in new[] { _identificacion, _nombre, _email, _telefono }) t.Clear();
        _identificacion.ReadOnly = false;
        _categoria.SelectedIndex = 0;
        _identificacion.Focus();
    }

    private async Task GuardarAsync()
    {
        if (!Validar(_identificacion, _identificacion.Text.Trim().Length > 0, "Identificación obligatoria") ||
            !Validar(_nombre, _nombre.Text.Trim().Length > 0, "Nombre obligatorio") ||
            !Validar(_email, _email.Text.Contains('@'), "Email inválido")) return;
        var datos = new ClienteEdicion(_idActual, _identificacion.Text, _nombre.Text, _email.Text, _telefono.Text, (CategoriaCliente)_categoria.SelectedItem!);
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioClientes>().GuardarAsync(datos));
        if (!Ejecutor.Mostrar(r)) return;
        _idActual = r!.Valor;
        await RefrescarAsync();
        await NotificarAsync(EventoVista.ClientesCambiados);
    }

    private async Task AlternarAsync()
    {
        if (Ui.Seleccionado<ClienteDto>(_grilla) is not { } c) return;
        if (!Ui.Confirmar($"¿{(c.Activo ? "Desactivar" : "Activar")} al cliente {c.Nombre}?")) return;
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<ServicioClientes>().CambiarEstadoAsync(c.Id, !c.Activo));
        if (Ejecutor.Mostrar(r)) { await RefrescarAsync(); await NotificarAsync(EventoVista.ClientesCambiados); }
    }
}
