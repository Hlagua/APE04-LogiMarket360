using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Eventos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Notificaciones;

//patron factory method (creacional)
//clase CreadorNotificador
//rol: creador abstracto
//objetivo: NotificarAsync es la operacion estable que usa el producto
//cada creador concreto decide QUE notificador crear en CrearNotificador
public abstract class CreadorNotificador
{
    public abstract string Canal { get; }

    //metodo de fabrica
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

//elige el creador segun la configuracion (fuera de los formularios)
//la UI nunca instancia notificadores concretos
public sealed class SelectorCanalNotificacion : ISelectorCanalNotificacion
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
