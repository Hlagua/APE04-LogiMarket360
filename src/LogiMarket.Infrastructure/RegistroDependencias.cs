using LogiMarket.Application.Administracion;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Clientes;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Logistica;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Reportes;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Promociones;
using LogiMarket.Infrastructure.Documentos;
using LogiMarket.Infrastructure.Notificaciones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Persistencia;
using LogiMarket.Infrastructure.Registro;
using LogiMarket.Infrastructure.Seguridad;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Infrastructure;

public sealed class OpcionesLogiMarket
{
    //ruta del archivo SQLite
    public required string RutaBaseDatos { get; init; }

    //carpeta de comprobantes TXT y notificaciones simuladas
    public required string CarpetaSalidas { get; init; }

    //carpeta de la bitacora tecnica
    //null = solo en memoria
    public string? CarpetaLogs { get; init; }
}

//registro de dependencias de Application + Infrastructure (lo usan el composition root de WinForms
//las pruebas)
//toda colaboracion se resuelve por constructor (DIP)
public static class RegistroDependencias
{
    public static IServiceCollection AgregarLogiMarket(this IServiceCollection s, OpcionesLogiMarket o)
    {
        var cadena = new SqliteConnectionStringBuilder { DataSource = o.RutaBaseDatos, DefaultTimeout = 30, Pooling = false }.ToString();

        //singletons de sesion
        s.AddSingleton(o);
        s.AddSingleton<IReloj, RelojSistema>();
        s.AddSingleton<IBitacoraTecnica>(_ => new BitacoraArchivo(o.CarpetaLogs));
        s.AddSingleton<ISesionActual, SesionActual>();
        s.AddSingleton<IPoliticaPermisos, PoliticaPermisos>();
        s.AddSingleton<ConfiguracionDemo>();
        s.AddSingleton<IMetricasSesion>(MetricasSesion.Instancia); //singleton: DI registra la UNICA instancia que controla la clase
        s.AddSingleton<IInvocadorComandos, InvocadorComandos>();
        s.AddSingleton<IFabricaMetadatosSku, CatalogoVisualFlyweightFactory>(); //fabrica Flyweight compartida
        s.AddSingleton<IGeneradorVistaMasiva, GeneradorVistaMasiva>();
        s.AddSingleton<IHasherContrasena, HasherContrasenaPbkdf2>();

        //strategy, Abstract Factory, Chain
        s.AddSingleton<IPoliticaDescuento, SinDescuento>();
        s.AddSingleton<IPoliticaDescuento, Vip10>();
        s.AddSingleton<IPoliticaDescuento, Mayorista15>();
        s.AddSingleton<ICalculadoraDescuento>(sp => new CalculadoraDescuento(sp.GetServices<IPoliticaDescuento>()));
        s.AddSingleton<IFabricaEntrega, EntregaEstandarFactory>();
        s.AddSingleton<IFabricaEntrega, EntregaExpresFactory>();
        s.AddSingleton<IProveedorFabricasEntrega, ProveedorFabricasEntrega>();
        s.AddSingleton<IServicioValoracion, ServicioValoracion>();
        s.AddSingleton<ICadenaValidacionPedido, CadenaValidacionPedido>();

        //persistencia (Unit of Work = DbContext por operacion)
        s.AddDbContext<LogiMarketDbContext>(b => b.UseSqlite(cadena), ServiceLifetime.Scoped);
        s.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();
        s.AddScoped<IInicializadorBaseDatos, InicializadorBaseDatos>();
        s.AddScoped<IConsultasLectura, ConsultasLecturaEf>();
        s.AddScoped<IProductoRepositorio, ProductoRepositorioEf>();
        s.AddScoped<IKitRepositorio, KitRepositorioEf>();
        s.AddScoped<IClienteRepositorio, ClienteRepositorioEf>();
        s.AddScoped<IPedidoRepositorio, PedidoRepositorioEf>();
        s.AddScoped<IPagoRepositorio, PagoRepositorioEf>();
        s.AddScoped<IMovimientoInventarioRepositorio, MovimientoInventarioRepositorioEf>();
        s.AddScoped<IDespachoRepositorio, DespachoRepositorioEf>();
        s.AddScoped<IDevolucionRepositorio, DevolucionRepositorioEf>();
        s.AddScoped<IOutboxRepositorio, OutboxRepositorioEf>();
        s.AddScoped<IAuditoriaRepositorio, AuditoriaRepositorioEf>();
        s.AddScoped<IUsuarioRepositorio, UsuarioRepositorioEf>();

        //cobro: Proxy -> Decorator -> (Adapter legado | proveedor moderno)
        s.AddSingleton(sp =>
        {
            var demo = sp.GetRequiredService<ConfiguracionDemo>();
            var bitacora = sp.GetRequiredService<IBitacoraTecnica>();
            return new PasarelaLegadaSimulada(() => demo.ForzarRechazoCobro, () => demo.ForzarErrorPasarela,
                m => bitacora.Registrar("PAGO", m));
        });
        s.AddSingleton<PasarelaLegadaAdapter>();
        s.AddSingleton<PasarelaModernaSimulada>();
        s.AddSingleton(sp => new SelectorPasarela(new IPasarelaPago[]
        {
            sp.GetRequiredService<PasarelaLegadaAdapter>(), sp.GetRequiredService<PasarelaModernaSimulada>()
        }, sp.GetRequiredService<ConfiguracionDemo>()));
        s.AddScoped<IPasarelaPago>(sp => new PagoAutorizadoProxy(
            new AuditoriaPagoDecorator(sp.GetRequiredService<SelectorPasarela>().Actual, sp.GetRequiredService<IBitacoraTecnica>()),
            sp.GetRequiredService<ISesionActual>(), sp.GetRequiredService<IPoliticaPermisos>(),
            sp.GetRequiredService<IBitacoraTecnica>(), sp.GetRequiredService<IMetricasSesion>()));
        s.AddSingleton<IServicioReembolso, ServicioReembolsoSimulado>();

        //avisos (Factory Method) y Observer + Outbox
        s.AddSingleton<CreadorNotificador>(sp => new CreadorNotificadorEmail(o.CarpetaSalidas,
            sp.GetRequiredService<ConfiguracionDemo>(), sp.GetRequiredService<IBitacoraTecnica>()));
        s.AddSingleton<CreadorNotificador>(sp => new CreadorNotificadorSms(o.CarpetaSalidas,
            sp.GetRequiredService<ConfiguracionDemo>(), sp.GetRequiredService<IBitacoraTecnica>()));
        s.AddSingleton<ISelectorCanalNotificacion, SelectorCanalNotificacion>();
        s.AddScoped<IObservadorEventos, ObservadorNotificaciones>();
        s.AddScoped<IObservadorEventos, ObservadorAuditoria>();
        s.AddScoped<IObservadorEventos, ObservadorMetricas>();
        s.AddScoped<IPublicadorEventos, PublicadorEventos>();
        s.AddScoped<IProcesadorOutbox, ProcesadorOutbox>();

        //salidas de documentos (Bridge)
        //VistaPrevia la registra WinForms
        s.AddSingleton<ISalidaDocumento>(_ => new SalidaArchivoTxt(Path.Combine(o.CarpetaSalidas, "comprobantes")));

        //casos de uso
        s.AddScoped<IServicioAutorizacion, ServicioAutorizacion>();
        s.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
        s.AddScoped<IServicioConsultas, ServicioConsultas>(); //lado de lectura (CQRS)
        s.AddScoped<IServicioCatalogo, ServicioCatalogo>();
        s.AddScoped<IServicioClientes, ServicioClientes>();
        s.AddScoped<IServicioPedidos, ServicioPedidos>();
        s.AddScoped<RepositoriosConfirmacion>();
        s.AddScoped<IConfirmacionPedido, ConfirmacionPedidoFacade>();
        s.AddScoped<IServicioLogistica, ServicioLogistica>();
        s.AddScoped<DependenciasDevolucion>();
        s.AddScoped<ProcesarDevolucion, ProcesarDevolucionDefecto>();
        s.AddScoped<ProcesarDevolucion, ProcesarDevolucionArrepentimiento>();
        s.AddScoped<IServicioDevoluciones, ServicioDevoluciones>();
        s.AddScoped<IServicioReportes, ServicioReportes>();
        s.AddScoped<IServicioAdministracion, ServicioAdministracion>();
        return s;
    }
}
