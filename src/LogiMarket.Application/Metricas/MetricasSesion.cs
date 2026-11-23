namespace LogiMarket.Application.Metricas;

public sealed record InstantaneaMetricas(Guid InstanciaId, DateTime Inicio, int PedidosConfirmados, int CobrosRechazados,
    int ConfirmacionesRepetidas, int Despachos, int Entregas, int Devoluciones, int Notificaciones, int EventosProcesados,
    int OperacionesDenegadas, decimal MontoCobrado);

//contrato de las metricas de sesion (los consumidores dependen de la abstraccion, DIP)
public interface IMetricasSesion
{
    Guid InstanciaId { get; }
    DateTime Inicio { get; }
    event Action<InstantaneaMetricas>? Cambiaron;
    void RegistrarConfirmacion(decimal monto);
    void RegistrarCobroRechazado();
    void RegistrarConfirmacionRepetida();
    void RegistrarDespacho();
    void RegistrarEntrega();
    void RegistrarDevolucion();
    void RegistrarNotificacion();
    void RegistrarDenegacion();
    bool RegistrarEventoProcesado(int eventoId);
    InstantaneaMetricas Instantanea();
}

//patron singleton (creacional)
//clase MetricasSesion
//objetivo: la propia clase impide una segunda instancia (constructor privado)
//expone la unica por Instancia (inicializacion perezosa y segura para hilos)
//el contenedor DI registra ESA instancia (AddSingleton(MetricasSesion.Instancia))
//de modo que DI y cualquier otro acceso comparten el mismo objeto
//contadores de la SESION con lock
//la base de datos sigue siendo la fuente de verdad
public sealed class MetricasSesion : IMetricasSesion
{
    private static readonly Lazy<MetricasSesion> Unica = new(() => new MetricasSesion(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static MetricasSesion Instancia => Unica.Value;

    private readonly object _candado = new();
    private readonly HashSet<int> _eventosContados = new();
    private int _confirmados, _rechazados, _repetidas, _despachos, _entregas, _devoluciones, _notificaciones, _eventos, _denegadas;
    private decimal _monto;

    private MetricasSesion() { }

    public Guid InstanciaId { get; } = Guid.NewGuid();
    public DateTime Inicio { get; private set; } = DateTime.Now;

    //pone los contadores en cero sin crear otra instancia
    //solo para pruebas automatizadas (cada prueba parte de una base restablecida
    //necesita contadores limpios)
    internal void Reiniciar()
    {
        lock (_candado)
        {
            _eventosContados.Clear();
            _confirmados = _rechazados = _repetidas = _despachos = _entregas = _devoluciones = _notificaciones = _eventos = _denegadas = 0;
            _monto = 0;
            Inicio = DateTime.Now;
        }
    }

    //se dispara tras cada cambio (el Mediator de la UI lo usa para refrescar la vista de metricas)
    public event Action<InstantaneaMetricas>? Cambiaron;

    public void RegistrarConfirmacion(decimal monto) => Modificar(() => { _confirmados++; _monto += monto; });
    public void RegistrarCobroRechazado() => Modificar(() => _rechazados++);
    public void RegistrarConfirmacionRepetida() => Modificar(() => _repetidas++);
    public void RegistrarDespacho() => Modificar(() => _despachos++);
    public void RegistrarEntrega() => Modificar(() => _entregas++);
    public void RegistrarDevolucion() => Modificar(() => _devoluciones++);
    public void RegistrarNotificacion() => Modificar(() => _notificaciones++);
    public void RegistrarDenegacion() => Modificar(() => _denegadas++);

    //cuenta un evento de Outbox una sola vez aunque se reintente su publicacion
    //un evento sin id de Outbox (publicado solo en memoria) no se puede deduplicar: se cuenta siempre
    public bool RegistrarEventoProcesado(int eventoId) =>
        Modificar(() =>
        {
            var nuevo = eventoId <= 0 || _eventosContados.Add(eventoId);
            if (nuevo) _eventos++;
            return nuevo;
        });

    public InstantaneaMetricas Instantanea()
    {
        lock (_candado) return InstantaneaSinBloqueo();
    }

    private InstantaneaMetricas InstantaneaSinBloqueo() =>
        new(InstanciaId, Inicio, _confirmados, _rechazados, _repetidas, _despachos,
            _entregas, _devoluciones, _notificaciones, _eventos, _denegadas, _monto);

    private void Modificar(Action accion) => Modificar(() => { accion(); return true; });

    //aplica el cambio y toma la instantanea en un solo bloqueo
    //avisa solo si algo cambio
    private bool Modificar(Func<bool> accion)
    {
        bool cambio;
        InstantaneaMetricas foto;
        lock (_candado)
        {
            cambio = accion();
            foto = InstantaneaSinBloqueo();
        }
        if (cambio) Cambiaron?.Invoke(foto);
        return cambio;
    }
}
