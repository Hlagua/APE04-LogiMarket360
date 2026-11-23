using LogiMarket.Application.Comun;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Application.Administracion;

//comandos de la pantalla de demostracion (solo ADMIN): proveedor y canal simulados, fallos inducidos
//procesamiento manual del Outbox y restablecimiento de la base de pruebas
//las consultas operativas (inventario, auditoria, bitacora, metricas) estan en ServicioConsultas
public sealed class ServicioAdministracion : IServicioAdministracion
{
    private static readonly string[] Proveedores = { ConfiguracionDemo.ProveedorLegado, ConfiguracionDemo.ProveedorModerno };
    private static readonly string[] Canales = { ConfiguracionDemo.CanalEmail, ConfiguracionDemo.CanalSms };

    private readonly IServicioAutorizacion _autorizacion;
    private readonly IBitacoraTecnica _bitacora;
    private readonly ConfiguracionDemo _demo;
    private readonly IProcesadorOutbox _procesadorOutbox;
    private readonly IInicializadorBaseDatos _inicializador;
    private readonly ISesionActual _sesion;

    public ServicioAdministracion(IServicioAutorizacion autorizacion, IBitacoraTecnica bitacora, ConfiguracionDemo demo,
        IProcesadorOutbox procesadorOutbox, IInicializadorBaseDatos inicializador, ISesionActual sesion)
    {
        _autorizacion = autorizacion;
        _bitacora = bitacora;
        _demo = demo;
        _procesadorOutbox = procesadorOutbox;
        _inicializador = inicializador;
        _sesion = sesion;
    }

    public ConfiguracionDemo ConfiguracionActual
    {
        get
        {
            _autorizacion.Exigir(Operacion.AdministrarDemo);
            return _demo;
        }
    }

    //aplica el cambio sobre una copia y la valida antes de publicarla:
    //un proveedor o canal desconocido se rechaza en lugar de reemplazarse en silencio por un valor por defecto
    public void AplicarDemo(Action<ConfiguracionDemo> cambio)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        var propuesta = new ConfiguracionDemo
        {
            ProveedorPago = _demo.ProveedorPago,
            CanalNotificacion = _demo.CanalNotificacion,
            ForzarRechazoCobro = _demo.ForzarRechazoCobro,
            ForzarErrorPasarela = _demo.ForzarErrorPasarela,
            ForzarFalloNotificador = _demo.ForzarFalloNotificador
        };
        cambio(propuesta);
        if (!Proveedores.Contains(propuesta.ProveedorPago))
            throw new ExcepcionAplicacion("PROVEEDOR_DESCONOCIDO", $"Proveedor de cobro no válido: '{propuesta.ProveedorPago}'.");
        if (!Canales.Contains(propuesta.CanalNotificacion))
            throw new ExcepcionAplicacion("CANAL_DESCONOCIDO", $"Canal de aviso no válido: '{propuesta.CanalNotificacion}'.");

        _demo.ProveedorPago = propuesta.ProveedorPago;
        _demo.CanalNotificacion = propuesta.CanalNotificacion;
        _demo.ForzarRechazoCobro = propuesta.ForzarRechazoCobro;
        _demo.ForzarErrorPasarela = propuesta.ForzarErrorPasarela;
        _demo.ForzarFalloNotificador = propuesta.ForzarFalloNotificador;
        _bitacora.Registrar("DEMO", $"Configuración de demostración cambiada por {_sesion.NombreUsuario}: {_demo}");
    }

    public async Task<ResultadoProcesoOutbox> ProcesarOutboxAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        return await _procesadorOutbox.ProcesarPendientesAsync(50, ct);
    }

    //restablece la base de PRUEBAS (borra, migra y siembra)
    //solo ADMIN y con confirmacion en la UI
    public async Task RestablecerBaseAsync(CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.AdministrarDemo);
        _bitacora.Registrar("DEMO", $"Restablecimiento de base solicitado por {_sesion.NombreUsuario}");
        await _inicializador.RestablecerAsync(ct);
    }
}
