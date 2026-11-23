using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Formularios;

//01
//acceso: identificacion local con usuario y contraseña (hash PBKDF2 en la base)
public sealed class FormLogin : Form
{
    private readonly Ejecutor _ejecutor;
    private readonly TextBox _usuario = new() { Width = 220 };
    private readonly TextBox _clave = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _entrar;

    public FormLogin(Ejecutor ejecutor, string baseDatos)
    {
        _ejecutor = ejecutor;
        Text = "LogiMarket 360 - Acceso";
        Font = Ui.FuenteNormal;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;

        _entrar = Ui.Boton("Iniciar sesión", async (_, _) => await EntrarAsync(), true);
        var cancelar = Ui.Boton("Salir", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });

        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(20), Dock = DockStyle.Fill };
        t.Controls.Add(Ui.Titulo("LogiMarket 360"), 0, 0);
        t.SetColumnSpan(t.GetControlFromPosition(0, 0)!, 2);
        t.Controls.Add(new Label { Text = "Ventas y logística - acceso local", AutoSize = true, ForeColor = Color.DimGray }, 0, 1);
        t.SetColumnSpan(t.GetControlFromPosition(0, 1)!, 2);
        t.Controls.Add(Ui.Etiqueta("&Usuario"), 0, 2);
        t.Controls.Add(_usuario, 1, 2);
        t.Controls.Add(Ui.Etiqueta("&Contraseña"), 0, 3);
        t.Controls.Add(_clave, 1, 3);
        t.Controls.Add(_error, 1, 4);
        var botones = Ui.Barra(_entrar, cancelar);
        botones.Dock = DockStyle.None;
        t.Controls.Add(botones, 1, 5);
        var pie = new Label
        {
            AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(380, 0),
            Text = $"Cuentas de demostración: admin, ventas, bodega, consulta (ver README).\nBase: {baseDatos}"
        };
        t.Controls.Add(pie, 0, 6);
        t.SetColumnSpan(pie, 2);
        Controls.Add(t);

        _usuario.TabIndex = 0;
        _clave.TabIndex = 1;
        _entrar.TabIndex = 2;
        cancelar.TabIndex = 3;
        AcceptButton = _entrar;
        CancelButton = cancelar;
    }

    public UsuarioSesion? Usuario { get; private set; }

    private async Task EntrarAsync()
    {
        _error.Text = "";
        if (_usuario.Text.Trim().Length == 0) { _error.Text = "Ingrese el usuario."; _usuario.Focus(); return; }
        if (_clave.Text.Length == 0) { _error.Text = "Ingrese la contraseña."; _clave.Focus(); return; }
        _entrar.Enabled = false;
        var (ok, r) = await _ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioAutenticacion>().IniciarSesionAsync(_usuario.Text, _clave.Text));
        _entrar.Enabled = true;
        if (!ok || r is null) return;
        if (!r.Exito)
        {
            _error.Text = r.Mensaje;
            _clave.SelectAll();
            _clave.Focus();
            return;
        }
        Usuario = r.Valor;
        DialogResult = DialogResult.OK;
        Close();
    }
}
