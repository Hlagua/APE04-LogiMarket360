using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;

namespace LogiMarket.WinForms.Vistas;

/// <summary>Base de las pantallas (colegas del Mediator). Habilitar botones por rol es solo ayuda visual:
/// la autorización real la aplica Application.</summary>
public abstract class VistaBase : UserControl, IVistaColega
{
    protected readonly Ejecutor Ejecutor;
    protected readonly IMediadorVistas Mediador;
    protected readonly ErrorProvider Errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    protected VistaBase(Ejecutor ejecutor, IMediadorVistas mediador, string clave)
    {
        Ejecutor = ejecutor;
        Mediador = mediador;
        Clave = clave;
        Dock = DockStyle.Fill;
        Font = Ui.FuenteNormal;
        BackColor = Ui.ColorFondo;
    }

    public string Clave { get; }

    public abstract Task RefrescarAsync();

    public virtual Task RecibirPedidoAsync(int pedidoId) => Task.CompletedTask;

    protected UsuarioSesion? Usuario => Ejecutor.Singleton<ISesionActual>().Usuario;

    protected bool Puede(Operacion operacion) => Ejecutor.Singleton<PoliticaPermisos>().Permite(Usuario?.Rol, operacion);

    protected Task NotificarAsync(EventoVista evento, int pedidoId = 0) => Mediador.NotificarAsync(this, evento, pedidoId);

    /// <summary>Validación cercana al campo: marca el control y devuelve false si la condición falla.</summary>
    protected bool Validar(Control control, bool condicion, string mensaje)
    {
        Errores.SetError(control, condicion ? "" : mensaje);
        if (!condicion) control.Focus();
        return condicion;
    }
}
