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

    /// <summary>
    /// Suscribe la vista a las métricas (Singleton) y se desuscribe al destruirse, para que una vista de
    /// una sesión cerrada no quede retenida ni intente actualizar controles eliminados.
    /// </summary>
    protected void AlCambiarMetricas(Action<Application.Metricas.InstantaneaMetricas> accion)
    {
        var metricas = Ejecutor.Singleton<Application.Metricas.MetricasSesion>();
        void Manejador(Application.Metricas.InstantaneaMetricas m)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(() => { if (!IsDisposed) accion(m); }); }
            catch (InvalidOperationException) { /* el control se destruyó entre la verificación y la llamada */ }
        }
        metricas.Cambiaron += Manejador;
        Disposed += (_, _) => metricas.Cambiaron -= Manejador;
    }

    protected Task NotificarAsync(EventoVista evento, int pedidoId = 0) => Mediador.NotificarAsync(this, evento, pedidoId);

    /// <summary>Validación cercana al campo: marca el control y devuelve false si la condición falla.</summary>
    protected bool Validar(Control control, bool condicion, string mensaje)
    {
        Errores.SetError(control, condicion ? "" : mensaje);
        if (!condicion) control.Focus();
        return condicion;
    }
}
