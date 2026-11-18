using LogiMarket.Application.Comun;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Infrastructure.Notificaciones;

/// <summary>Base de los notificadores simulados: escriben un registro real en salidas/notificaciones.log.</summary>
public abstract class NotificadorSimuladoBase : INotificador
{
    private static readonly object Candado = new();
    private readonly string _carpeta;
    private readonly ConfiguracionDemo _demo;
    protected readonly IBitacoraTecnica Bitacora;

    protected NotificadorSimuladoBase(string carpeta, ConfiguracionDemo demo, IBitacoraTecnica bitacora)
    {
        _carpeta = carpeta;
        _demo = demo;
        Bitacora = bitacora;
    }

    public abstract string Canal { get; }

    protected abstract string Formatear(string destinatario, string asunto, string mensaje);

    public Task EnviarAsync(string destinatario, string asunto, string mensaje, CancellationToken ct = default)
    {
        if (_demo.ForzarFalloNotificador)
            throw new InvalidOperationException($"Servidor de {Canal} no disponible (fallo simulado).");
        var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {Formatear(destinatario, asunto, mensaje)}";
        lock (Candado)
        {
            Directory.CreateDirectory(_carpeta);
            File.AppendAllText(Path.Combine(_carpeta, "notificaciones.log"), linea + Environment.NewLine);
        }
        Bitacora.Registrar("NOTIFICACION", $"{GetType().Name}: {linea}");
        return Task.CompletedTask;
    }
}

public sealed class NotificadorEmailSimulado : NotificadorSimuladoBase
{
    public NotificadorEmailSimulado(string carpeta, ConfiguracionDemo demo, IBitacoraTecnica bitacora) : base(carpeta, demo, bitacora) { }
    public override string Canal => ConfiguracionDemo.CanalEmail;
    protected override string Formatear(string d, string a, string m) => $"[EMAIL] Para: {d} | Asunto: {a} | {m}";
}

public sealed class NotificadorSmsSimulado : NotificadorSimuladoBase
{
    public NotificadorSmsSimulado(string carpeta, ConfiguracionDemo demo, IBitacoraTecnica bitacora) : base(carpeta, demo, bitacora) { }
    public override string Canal => ConfiguracionDemo.CanalSms;
    protected override string Formatear(string d, string a, string m) => $"[SMS] Tel: {d} | {(m.Length > 140 ? m[..140] : m)}";
}

/// <summary>Creador concreto EMAIL: su método de fábrica entrega un notificador de correo.</summary>
public sealed class CreadorNotificadorEmail : CreadorNotificador
{
    private readonly string _carpeta;
    private readonly ConfiguracionDemo _demo;
    private readonly IBitacoraTecnica _bitacora;

    public CreadorNotificadorEmail(string carpeta, ConfiguracionDemo demo, IBitacoraTecnica bitacora)
    {
        _carpeta = carpeta;
        _demo = demo;
        _bitacora = bitacora;
    }

    public override string Canal => ConfiguracionDemo.CanalEmail;
    protected override INotificador CrearNotificador() => new NotificadorEmailSimulado(_carpeta, _demo, _bitacora);
    protected override string ObtenerDestinatario(EventoNegocio e) => string.IsNullOrWhiteSpace(e.ClienteEmail) ? "sin-email" : e.ClienteEmail;
}

/// <summary>Creador concreto SMS: su método de fábrica entrega un notificador SMS.</summary>
public sealed class CreadorNotificadorSms : CreadorNotificador
{
    private readonly string _carpeta;
    private readonly ConfiguracionDemo _demo;
    private readonly IBitacoraTecnica _bitacora;

    public CreadorNotificadorSms(string carpeta, ConfiguracionDemo demo, IBitacoraTecnica bitacora)
    {
        _carpeta = carpeta;
        _demo = demo;
        _bitacora = bitacora;
    }

    public override string Canal => ConfiguracionDemo.CanalSms;
    protected override INotificador CrearNotificador() => new NotificadorSmsSimulado(_carpeta, _demo, _bitacora);
    protected override string ObtenerDestinatario(EventoNegocio e) => string.IsNullOrWhiteSpace(e.ClienteTelefono) ? "sin-telefono" : e.ClienteTelefono;
}
