using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Logistica;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;

namespace LogiMarket.Application.Puertos;

// Interfaces PEQUEÑAS por agregado (ISP). Implementadas con EF Core en Infrastructure (DIP).

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
    Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default);
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
    Task<Despacho?> ObtenerPorGuiaAsync(string guia, CancellationToken ct = default);
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

/// <summary>
/// Unit of Work: materializado por el DbContext y una transacción real de base de datos.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);

    /// <summary>Ejecuta la operación en una transacción; confirma si termina bien y revierte si lanza.</summary>
    Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default);

    /// <summary>Descarta los cambios en memoria no guardados (tras un rollback).</summary>
    void DescartarCambios();
}

/// <summary>Creación, migración, semilla idempotente y restablecimiento controlado de la base.</summary>
public interface IInicializadorBaseDatos
{
    Task InicializarAsync(CancellationToken ct = default);
    Task RestablecerAsync(CancellationToken ct = default);
    string Descripcion { get; }
}
