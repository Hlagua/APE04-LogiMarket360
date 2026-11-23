using System.Text.Json;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Eventos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Eventos;

/// <summary>Convierte eventos de negocio a/desde el payload JSON guardado en el Outbox.</summary>
public static class SerializadorEventos
{
    private static readonly Dictionary<string, Type> Tipos = new()
    {
        [PedidoPagado.NombreTipo] = typeof(PedidoPagado),
        [DespachoRegistrado.NombreTipo] = typeof(DespachoRegistrado),
        [DevolucionAceptada.NombreTipo] = typeof(DevolucionAceptada)
    };

    public static EventoOutbox AOutbox(EventoNegocio evento) =>
        new(evento.Tipo, evento.PedidoId, JsonSerializer.Serialize(evento, evento.GetType()), evento.Fecha);

    public static EventoNegocio DesdeOutbox(EventoOutbox registro)
    {
        if (!Tipos.TryGetValue(registro.Tipo, out var tipo))
            throw new ExcepcionAplicacion("EVENTO_DESCONOCIDO", $"Tipo de evento no soportado: {registro.Tipo}");
        var evento = (EventoNegocio?)JsonSerializer.Deserialize(registro.Payload, tipo)
                     ?? throw new ExcepcionAplicacion("EVENTO_INVALIDO", $"Payload inválido en evento {registro.Id}");
        return evento with { EventoOutboxId = registro.Id };
    }
}

/// <summary>Patrón Observer: suscriptor de eventos de negocio.</summary>
public interface IObservadorEventos
{
    string Nombre { get; }
    Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default);
}

/// <summary>Sujeto del Observer: notifica a todos los suscriptores, en orden de suscripción.</summary>
public sealed class PublicadorEventos : IPublicadorEventos
{
    private readonly List<IObservadorEventos> _observadores = new();

    public PublicadorEventos(IEnumerable<IObservadorEventos> observadores) => _observadores.AddRange(observadores);

    public IReadOnlyList<string> Suscriptores => _observadores.Select(o => o.Nombre).ToList();

    public void Suscribir(IObservadorEventos observador)
    {
        if (!_observadores.Contains(observador)) _observadores.Add(observador);
    }

    public void Desuscribir(IObservadorEventos observador) => _observadores.Remove(observador);

    public async Task PublicarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        foreach (var observador in _observadores.ToList())
            await observador.ActualizarAsync(evento, ct);
    }
}

/// <summary>
/// Observador 1: envía el aviso por el canal configurado (usa el Factory Method). Es idempotente por
/// evento: apenas el aviso sale, guarda una constancia (categoría NOTIFICACION) en su propia escritura.
/// Si después falla otro observador y el evento se reintenta, el aviso no se vuelve a enviar.
/// </summary>
public sealed class ObservadorNotificaciones : IObservadorEventos
{
    public const string Categoria = "NOTIFICACION";
    private readonly ISelectorCanalNotificacion _selector;
    private readonly IMetricasSesion _metricas;
    private readonly IBitacoraTecnica _bitacora;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;

    public ObservadorNotificaciones(ISelectorCanalNotificacion selector, IMetricasSesion metricas, IBitacoraTecnica bitacora,
        IAuditoriaRepositorio auditoria, IUnidadDeTrabajo uow, IReloj reloj)
    {
        _selector = selector;
        _metricas = metricas;
        _bitacora = bitacora;
        _auditoria = auditoria;
        _uow = uow;
        _reloj = reloj;
    }

    public string Nombre => "Notificación";

    public async Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        var deOutbox = evento.EventoOutboxId > 0;
        if (deOutbox && await _auditoria.ExisteParaEventoAsync(evento.EventoOutboxId, Categoria, ct))
        {
            _bitacora.Registrar("OBSERVER", $"Aviso del evento {evento.EventoOutboxId} ya enviado: no se repite.");
            return;
        }

        var creador = _selector.Actual;
        var detalle = await creador.NotificarAsync(evento, ct);
        _bitacora.Registrar("OBSERVER", $"Notificación {evento.Tipo} evento={evento.EventoOutboxId} por {creador.GetType().Name}: {detalle}");
        _metricas.RegistrarNotificacion();

        if (!deOutbox) return;
        _auditoria.Agregar(new Domain.Eventos.Auditoria(_reloj.Ahora, "sistema", Categoria, evento.Tipo,
            $"Aviso por {creador.Canal}: {detalle}", evento.EventoOutboxId));
        await _uow.GuardarCambiosAsync(ct); // constancia inmediata, independiente del resto de observadores
    }
}

/// <summary>Observador 2: deja constancia en la auditoría persistente (idempotente por evento).</summary>
public sealed class ObservadorAuditoria : IObservadorEventos
{
    public const string Categoria = "EVENTO";
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IReloj _reloj;

    public ObservadorAuditoria(IAuditoriaRepositorio auditoria, IReloj reloj)
    {
        _auditoria = auditoria;
        _reloj = reloj;
    }

    public string Nombre => "Auditoría";

    public async Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        if (evento.EventoOutboxId > 0 && await _auditoria.ExisteParaEventoAsync(evento.EventoOutboxId, Categoria, ct))
            return;
        _auditoria.Agregar(new Domain.Eventos.Auditoria(_reloj.Ahora, "sistema", Categoria, evento.Tipo,
            evento.Mensaje, evento.EventoOutboxId > 0 ? evento.EventoOutboxId : null));
    }
}

/// <summary>Observador 3: actualiza las métricas de sesión (Singleton), una vez por evento.</summary>
public sealed class ObservadorMetricas : IObservadorEventos
{
    private readonly IMetricasSesion _metricas;

    public ObservadorMetricas(IMetricasSesion metricas) => _metricas = metricas;

    public string Nombre => "Métricas";

    public Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        _metricas.RegistrarEventoProcesado(evento.EventoOutboxId);
        return Task.CompletedTask;
    }
}
