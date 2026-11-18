using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Notificaciones;

/// <summary>
/// Patrón Factory Method (creador abstracto). <see cref="NotificarAsync"/> es la operación estable que
/// usa el producto; cada creador concreto decide QUÉ notificador crear en <see cref="CrearNotificador"/>.
/// </summary>
public abstract class CreadorNotificador
{
    public abstract string Canal { get; }

    /// <summary>Método de fábrica.</summary>
    protected abstract INotificador CrearNotificador();

    protected abstract string ObtenerDestinatario(EventoNegocio evento);

    public async Task<string> NotificarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        var notificador = CrearNotificador();
        var destinatario = ObtenerDestinatario(evento);
        await notificador.EnviarAsync(destinatario, $"LogiMarket 360 - {evento.Tipo}", evento.Mensaje, ct);
        return $"{notificador.GetType().Name} → {destinatario}";
    }
}

/// <summary>
/// Elige el creador según la configuración (fuera de los formularios). La UI nunca instancia
/// notificadores concretos.
/// </summary>
public sealed class SelectorCanalNotificacion
{
    private readonly IReadOnlyDictionary<string, CreadorNotificador> _creadores;
    private readonly ConfiguracionDemo _configuracion;

    public SelectorCanalNotificacion(IEnumerable<CreadorNotificador> creadores, ConfiguracionDemo configuracion)
    {
        _creadores = creadores.ToDictionary(c => c.Canal, StringComparer.OrdinalIgnoreCase);
        _configuracion = configuracion;
    }

    public IEnumerable<string> CanalesDisponibles => _creadores.Keys;

    public CreadorNotificador Actual =>
        _creadores.TryGetValue(_configuracion.CanalNotificacion, out var c)
            ? c
            : throw new ExcepcionAplicacion("CANAL_DESCONOCIDO", $"Canal de aviso no registrado: {_configuracion.CanalNotificacion}");
}
