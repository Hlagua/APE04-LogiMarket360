using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Logistica;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;

namespace LogiMarket.Application.Puertos;

//LADO DE ESCRITURA (CQRS basico):
//interfaces PEQUEÑAS por agregado (ISP), implementadas con EF Core en
//infrastructure (DIP)
//solo cargan agregados para modificarlos y los agregan
//las consultas para pantallas
//y reportes van por IConsultasLectura (Consultas/ConsultasLectura.cs)

public interface IProductoRepositorio
{
    Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> ObtenerPorSkusAsync(IEnumerable<string> skus, CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken ct = default);
    Task<bool> ExisteSkuAsync(string sku, CancellationToken ct = default);
    void Agregar(Producto producto);
}

public interface IKitRepositorio
{
    Task<Kit?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Kit>> ListarAsync(CancellationToken ct = default);
    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct = default);
    void Agregar(Kit kit);
}

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteIdentificacionAsync(string identificacion, CancellationToken ct = default);
    void Agregar(Cliente cliente);
}

public interface IPedidoRepositorio
{
    Task<Pedido?> ObtenerAsync(int id, CancellationToken ct = default);
    void Agregar(Pedido pedido);
}

public interface IPagoRepositorio
{
    Task<Pago?> ObtenerPorClaveAsync(string claveIdempotencia, CancellationToken ct = default);
    Task<Pago?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
    void Agregar(Pago pago);
}

public interface IMovimientoInventarioRepositorio
{
    void Agregar(MovimientoInventario movimiento);
}

public interface IDespachoRepositorio
{
    Task<Despacho?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
    void Agregar(Despacho despacho);
}

public interface IDevolucionRepositorio
{
    Task<Devolucion?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default);
    void Agregar(Devolucion devolucion);
}

public interface IOutboxRepositorio
{
    Task<IReadOnlyList<EventoOutbox>> PendientesAsync(int maximo, CancellationToken ct = default);
    Task<EventoOutbox?> ObtenerAsync(int id, CancellationToken ct = default);
    void Agregar(EventoOutbox evento);
}

public interface IAuditoriaRepositorio
{
    Task<bool> ExisteParaEventoAsync(int eventoOrigenId, string categoria, CancellationToken ct = default);
    void Agregar(Auditoria auditoria);
}

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken ct = default);
}

//unit of Work: materializado por el DbContext y una transaccion real de base de datos
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);

    //ejecuta la operacion en una transaccion
    //confirma si termina bien y revierte si lanza
    Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default);

    //descarta los cambios en memoria no guardados (tras un rollback)
    void DescartarCambios();
}

//creacion, migracion, semilla idempotente y restablecimiento controlado de la base
public interface IInicializadorBaseDatos
{
    Task InicializarAsync(CancellationToken ct = default);
    Task RestablecerAsync(CancellationToken ct = default);
    string Descripcion { get; }
}
