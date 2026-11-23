namespace LogiMarket.Application.Metricas;

public sealed record InstantaneaMetricas(Guid InstanciaId, DateTime Inicio, int PedidosConfirmados, int CobrosRechazados,
    int ConfirmacionesRepetidas, int Despachos, int Entregas, int Devoluciones, int Notificaciones, int EventosProcesados,
    int OperacionesDenegadas, decimal MontoCobrado);

/// <summary>Contrato de las métricas de sesión (los consumidores dependen de la abstracción, DIP).</summary>
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

/// <summary>
/// Patrón Singleton: la propia clase impide una segunda instancia (constructor privado) y expone la única
/// por <see cref="Instancia"/> (inicialización perezosa y segura para hilos). El contenedor DI registra
/// ESA instancia (AddSingleton(MetricasSesion.Instancia)), de modo que DI y cualquier otro acceso comparten
/// el mismo objeto. Contadores de la SESIÓN con lock; la base de datos sigue siendo la fuente de verdad.
/// </summary>
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

    /// <summary>
    /// Pone los contadores en cero sin crear otra instancia. Solo para pruebas automatizadas (cada prueba
    /// parte de una base restablecida y necesita contadores limpios).
    /// </summary>
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

    /// <summary>Se dispara tras cada cambio (el Mediator de la UI lo usa para refrescar la vista de métricas).</summary>
    public event Action<InstantaneaMetricas>? Cambiaron;

    public void RegistrarConfirmacion(decimal monto) => Modificar(() => { _confirmados++; _monto += monto; });
    public void RegistrarCobroRechazado() => Modificar(() => _rechazados++);
    public void RegistrarConfirmacionRepetida() => Modificar(() => _repetidas++);
    public void RegistrarDespacho() => Modificar(() => _despachos++);
    public void RegistrarEntrega() => Modificar(() => _entregas++);
    public void RegistrarDevolucion() => Modificar(() => _devoluciones++);
    public void RegistrarNotificacion() => Modificar(() => _notificaciones++);
    public void RegistrarDenegacion() => Modificar(() => _denegadas++);

    /// <summary>Cuenta un evento de Outbox una sola vez aunque se reintente su publicación.</summary>
    /// <remarks>Un evento sin id de Outbox (publicado solo en memoria) no se puede deduplicar: se cuenta siempre.</remarks>
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

    /// <summary>Aplica el cambio y toma la instantánea en un solo bloqueo; avisa solo si algo cambió.</summary>
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
