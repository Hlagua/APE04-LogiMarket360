using LogiMarket.Application.Seguridad;
using LogiMarket.WinForms.Infraestructura;

namespace LogiMarket.WinForms.Vistas;

//base de las pantallas (colegas del Mediator)
//habilitar botones por rol es solo ayuda visual: la autorizacion real la aplica Application
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

    protected bool Puede(Operacion operacion) => Ejecutor.Singleton<IPoliticaPermisos>().Permite(Usuario?.Rol, operacion);

    //suscribe la vista a las metricas (Singleton) y se desuscribe al destruirse
    //para que una vista de una sesion cerrada no quede retenida ni intente actualizar controles eliminados
    protected void AlCambiarMetricas(Action<Application.Metricas.InstantaneaMetricas> accion)
    {
        var metricas = Ejecutor.Singleton<Application.Metricas.IMetricasSesion>();
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

    //validacion cercana al campo: marca el control y devuelve false si la condicion falla
    protected bool Validar(Control control, bool condicion, string mensaje)
    {
        Errores.SetError(control, condicion ? "" : mensaje);
        if (!condicion) control.Focus();
        return condicion;
    }
}
