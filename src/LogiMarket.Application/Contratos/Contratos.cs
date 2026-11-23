using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Clientes;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Reportes;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Contratos;

// Contratos de los casos de uso y colaboradores (DIP): la presentación, los comandos y los demás servicios
// dependen de estas abstracciones, nunca de las clases concretas. Interfaces pequeñas por responsabilidad (ISP).

// ======================= LADO DE LECTURA (Query) =======================

/// <summary>Todas las consultas de la aplicación. Ninguna modifica datos (CQRS básico).</summary>
public interface IServicioConsultas
{
    Task<IReadOnlyList<ProductoDto>> ProductosAsync(string? texto = null, CancellationToken ct = default);
    Task<IReadOnlyList<KitDto>> KitsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ElementoVendibleDto>> VendiblesAsync(CancellationToken ct = default);
    Task<IElementoCatalogo> ElementoAsync(string codigo, CancellationToken ct = default);
    Task<DesgloseDto> DesgloseAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteDto>> ClientesAsync(string? texto = null, CancellationToken ct = default);
    Task<ClienteDto> ClienteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoResumenDto>> PedidosAsync(CancellationToken ct = default);
    Task<PedidoCompletoDto> PedidoAsync(int id, CancellationToken ct = default);
    Task<string> SeguimientoAsync(string guiaOPedido, CancellationToken ct = default);
    Task<IndicadoresDto> IndicadoresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, CancellationToken ct = default);
    Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(string? categoria, string? texto, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default);
    IReadOnlyList<EntradaBitacora> Bitacora(string? categoria);
    IReadOnlyList<RegistroComando> HistorialComandos();
    InstantaneaMetricas Metricas();
}

/// <summary>Reportes de solo lectura: filtro (Interpreter), comprobantes (Bridge) y vista masiva (Flyweight).</summary>
public interface IServicioReportes
{
    IEnumerable<string> MediosDisponibles { get; }
    Task<ResultadoFiltro> FiltrarPedidosAsync(string filtro, CancellationToken ct = default);
    Task<ResultadoSalida> EmitirComprobanteAsync(TipoComprobante tipo, int pedidoId, string medio, CancellationToken ct = default);
    Task<(IReadOnlyList<FilaCatalogoVisual> Filas, MedicionFlyweight Medicion)> VistaMasivaAsync(int filas = 1000, CancellationToken ct = default);
}

// ======================= LADO DE ESCRITURA (Command) =======================

public interface IServicioAutenticacion
{
    Task<ResultadoOperacion<UsuarioSesion>> IniciarSesionAsync(string nombreUsuario, string contrasena, CancellationToken ct = default);
    Task CerrarSesionAsync(CancellationToken ct = default);
}

public interface IServicioCatalogo
{
    Task<ResultadoOperacion> CrearProductoAsync(ProductoEdicion datos, CancellationToken ct = default);
    Task<ResultadoOperacion> ActualizarProductoAsync(ProductoEdicion datos, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarEstadoProductoAsync(string sku, bool activo, CancellationToken ct = default);
    Task<ResultadoOperacion> AjustarStockAsync(string sku, int nuevoStock, string motivo, CancellationToken ct = default);
    Task<ResultadoOperacion> CrearKitAsync(string codigo, string nombre, IReadOnlyList<(string Codigo, int Cantidad)> componentes, CancellationToken ct = default);
    Task<ResultadoOperacion> AgregarComponenteAsync(string codigoKit, string codigoComponente, int cantidad, CancellationToken ct = default);
    Task<ResultadoOperacion> QuitarComponenteAsync(string codigoKit, string codigoComponente, CancellationToken ct = default);
}

public interface IServicioClientes
{
    Task<ResultadoOperacion<int>> GuardarAsync(ClienteEdicion datos, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);
}

public interface IServicioPedidos
{
    EditorBorrador NuevoEditor();
    Task<EditorBorrador> AbrirEditorAsync(int pedidoId, CancellationToken ct = default);
    Task<ResultadoOperacion<int>> GuardarBorradorAsync(EditorBorrador editor, CancellationToken ct = default);
    Task<ResultadoValidacion> ValidarAsync(int pedidoId, CancellationToken ct = default);
    Task<ResultadoValidacion> DiagnosticarAsync(int pedidoId, CancellationToken ct = default);
    Task<ResultadoOperacion> CancelarAsync(int pedidoId, string motivo, CancellationToken ct = default);
    Task<ResultadoOperacion<int>> ClonarComoBorradorAsync(int pedidoId, CancellationToken ct = default);
}

/// <summary>Facade de confirmación (una sola operación).</summary>
public interface IConfirmacionPedido
{
    Task<ResultadoConfirmacion> ConfirmarAsync(int pedidoId, string claveIdempotencia, CancellationToken ct = default);
}

public interface IServicioLogistica
{
    Task<ResultadoOperacion<string>> DespacharAsync(int pedidoId, CancellationToken ct = default);
    Task<ResultadoOperacion> EntregarAsync(int pedidoId, CancellationToken ct = default);
}

public interface IServicioDevoluciones
{
    Task<ResultadoDevolucion> SolicitarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default);
}

/// <summary>Operaciones de la pantalla de demostración (solo ADMIN).</summary>
public interface IServicioAdministracion
{
    ConfiguracionDemo ConfiguracionActual { get; }
    void AplicarDemo(Action<ConfiguracionDemo> cambio);
    Task<ResultadoProcesoOutbox> ProcesarOutboxAsync(CancellationToken ct = default);
    Task RestablecerBaseAsync(CancellationToken ct = default);
}

public interface IInvocadorComandos
{
    IReadOnlyList<RegistroComando> Historial { get; }
    Task<ResultadoOperacion> EjecutarAsync(IComando comando, CancellationToken ct = default);
}

// ======================= COLABORADORES INTERNOS =======================

public interface IServicioValoracion
{
    ResumenValoracion Valorar(Pedido pedido);
}

public interface ICadenaValidacionPedido
{
    ResultadoValidacion Validar(ContextoValidacion contexto);
}

public interface IProveedorFabricasEntrega
{
    IFabricaEntrega Para(TipoEntrega tipo);
    PaqueteEntrega Preparar(TipoEntrega tipo, decimal pesoKg);
}

public interface IProcesadorOutbox
{
    Task<ResultadoProcesoOutbox> ProcesarPendientesAsync(int maximo = 50, CancellationToken ct = default);
    Task<ResultadoProcesoOutbox> ProcesarSinPropagarAsync(CancellationToken ct = default);
}

/// <summary>Sujeto del Observer.</summary>
public interface IPublicadorEventos
{
    IReadOnlyList<string> Suscriptores { get; }
    void Suscribir(IObservadorEventos observador);
    void Desuscribir(IObservadorEventos observador);
    Task PublicarAsync(EventoNegocio evento, CancellationToken ct = default);
}

public interface ISelectorCanalNotificacion
{
    IEnumerable<string> CanalesDisponibles { get; }
    CreadorNotificador Actual { get; }
}

/// <summary>Fábrica Flyweight de metadatos de SKU.</summary>
public interface IFabricaMetadatosSku
{
    int InstanciasCreadas { get; }
    MetadatosSku Obtener(string sku, string nombre, string categoria, string descripcion);
    void Limpiar();
}

public interface IGeneradorVistaMasiva
{
    IReadOnlyList<FilaCatalogoVisual> GenerarConFlyweight(IReadOnlyList<ProductoDto> productos, int filas);
    MedicionFlyweight Medir(IReadOnlyList<ProductoDto> productos, int filas = 1000);
}
