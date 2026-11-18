using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Application.Administracion;

/// <summary>Consultas operativas (inventario, auditoría, outbox, bitácora, métricas) y pantalla de demostración.</summary>
public sealed class ServicioAdministracion
{
    public const int UmbralStockBajo = 3;

    private readonly IConsultasLectura _consultas;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly IBitacoraTecnica _bitacora;
    private readonly MetricasSesion _metricas;
    private readonly InvocadorComandos _invocador;
    private readonly ConfiguracionDemo _demo;
    private readonly ProcesadorOutbox _procesadorOutbox;
    private readonly IInicializadorBaseDatos _inicializador;
    private readonly ISesionActual _sesion;

    public ServicioAdministracion(IConsultasLectura consultas, IServicioAutorizacion autorizacion, IBitacoraTecnica bitacora,
        MetricasSesion metricas, InvocadorComandos invocador, ConfiguracionDemo demo, ProcesadorOutbox procesadorOutbox,
        IInicializadorBaseDatos inicializador, ISesionActual sesion)
    {
        _consultas = consultas;
        _autorizacion = autorizacion;
        _bitacora = bitacora;
        _metricas = metricas;
        _invocador = invocador;
        _demo = demo;
        _procesadorOutbox = procesadorOutbox;
        _inicializador = inicializador;
        _sesion = sesion;
    }

    public Task<IndicadoresDto> IndicadoresAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _consultas.IndicadoresAsync(UmbralStockBajo, ct);
    }

    public Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        return _consultas.MovimientosAsync(sku, ct);
    }

    public Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(string? categoria, string? texto, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _consultas.AuditoriaAsync(categoria, texto, ct);
    }

    public Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _consultas.OutboxAsync(ct);
    }

    public IReadOnlyList<EntradaBitacora> Bitacora(string? categoria)
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _bitacora.Entradas(categoria).Reverse().ToList();
    }

    public IReadOnlyList<RegistroComando> HistorialComandos()
    {
        _autorizacion.Exigir(Operacion.VerAuditoria);
        return _invocador.Historial.Reverse().ToList();
    }

    public InstantaneaMetricas Metricas() => _metricas.Instantanea();

    // ---------------- Demostración (solo ADMIN) ----------------

    public ConfiguracionDemo ConfiguracionActual
    {
        get
        {
            _autorizacion.Exigir(Operacion.AdministrarDemo);
            return _demo;
        }
    }

    public void AplicarDemo(Action<ConfiguracionDemo> cambio)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        cambio(_demo);
        _bitacora.Registrar("DEMO", $"Configuración de demostración cambiada por {_sesion.NombreUsuario}: {_demo}");
    }

    public async Task<ResultadoProcesoOutbox> ProcesarOutboxAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        return await _procesadorOutbox.ProcesarPendientesAsync(50, ct);
    }

    /// <summary>Restablece la base de PRUEBAS (borra, migra y siembra). Solo ADMIN y con confirmación en la UI.</summary>
    public async Task RestablecerBaseAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        _bitacora.Registrar("DEMO", $"Restablecimiento de base solicitado por {_sesion.NombreUsuario}");
        await _inicializador.RestablecerAsync(ct);
    }
}
