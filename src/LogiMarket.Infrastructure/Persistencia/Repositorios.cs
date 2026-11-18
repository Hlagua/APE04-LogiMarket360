using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Logistica;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

// Adaptadores EF Core del patrón Repository. No llaman a SaveChanges: eso es de la Unidad de Trabajo.

public sealed class ProductoRepositorioEf(LogiMarketDbContext db) : IProductoRepositorio
{
    public Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default) =>
        db.Productos.FirstOrDefaultAsync(p => p.Sku == sku.Trim().ToUpper(), ct);

    public async Task<IReadOnlyList<Producto>> ObtenerPorSkusAsync(IEnumerable<string> skus, CancellationToken ct = default)
    {
        var lista = skus.Select(s => s.ToUpperInvariant()).Distinct().ToList();
        return await db.Productos.Where(p => lista.Contains(p.Sku)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken ct = default) =>
        await db.Productos.OrderBy(p => p.Sku).ToListAsync(ct);

    public Task<bool> ExisteSkuAsync(string sku, CancellationToken ct = default) =>
        db.Productos.AnyAsync(p => p.Sku == sku.Trim().ToUpper(), ct);

    public void Agregar(Producto producto) => db.Productos.Add(producto);
}

public sealed class KitRepositorioEf(LogiMarketDbContext db) : IKitRepositorio
{
    public Task<Kit?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default) =>
        db.Kits.Include(k => k.Componentes).FirstOrDefaultAsync(k => k.Codigo == codigo.Trim().ToUpper(), ct);

    public async Task<IReadOnlyList<Kit>> ListarAsync(CancellationToken ct = default) =>
        await db.Kits.Include(k => k.Componentes).OrderBy(k => k.Codigo).ToListAsync(ct);

    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct = default) =>
        db.Kits.AnyAsync(k => k.Codigo == codigo.Trim().ToUpper(), ct);

    public void Agregar(Kit kit) => db.Kits.Add(kit);
}

public sealed class ClienteRepositorioEf(LogiMarketDbContext db) : IClienteRepositorio
{
    public Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        var q = db.Clientes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(t) || c.Identificacion.Contains(t) || c.Email.ToLower().Contains(t));
        }
        return await q.OrderBy(c => c.Nombre).ToListAsync(ct);
    }

    public Task<bool> ExisteIdentificacionAsync(string identificacion, CancellationToken ct = default) =>
        db.Clientes.AnyAsync(c => c.Identificacion == identificacion.Trim(), ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);
}

public sealed class PedidoRepositorioEf(LogiMarketDbContext db) : IPedidoRepositorio
{
    public Task<Pedido?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Componentes)
            .Include(p => p.Historial)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Pedido pedido) => db.Pedidos.Add(pedido);
}

public sealed class PagoRepositorioEf(LogiMarketDbContext db) : IPagoRepositorio
{
    public Task<Pago?> ObtenerPorClaveAsync(string claveIdempotencia, CancellationToken ct = default) =>
        db.Pagos.FirstOrDefaultAsync(p => p.ClaveIdempotencia == claveIdempotencia, ct);

    public Task<Pago?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        db.Pagos.FirstOrDefaultAsync(p => p.PedidoId == pedidoId, ct);

    public void Agregar(Pago pago) => db.Pagos.Add(pago);
}

public sealed class MovimientoInventarioRepositorioEf(LogiMarketDbContext db) : IMovimientoInventarioRepositorio
{
    public void Agregar(MovimientoInventario movimiento) => db.MovimientosInventario.Add(movimiento);
}

public sealed class DespachoRepositorioEf(LogiMarketDbContext db) : IDespachoRepositorio
{
    public Task<Despacho?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        db.Despachos.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);

    public Task<Despacho?> ObtenerPorGuiaAsync(string guia, CancellationToken ct = default) =>
        db.Despachos.FirstOrDefaultAsync(d => d.NumeroGuia == guia, ct);

    public void Agregar(Despacho despacho) => db.Despachos.Add(despacho);
}

public sealed class DevolucionRepositorioEf(LogiMarketDbContext db) : IDevolucionRepositorio
{
    public Task<Devolucion?> ObtenerPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        db.Devoluciones.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);

    public void Agregar(Devolucion devolucion) => db.Devoluciones.Add(devolucion);
}

public sealed class OutboxRepositorioEf(LogiMarketDbContext db) : IOutboxRepositorio
{
    public async Task<IReadOnlyList<EventoOutbox>> PendientesAsync(int maximo, CancellationToken ct = default) =>
        await db.EventosOutbox.Where(e => e.FechaProcesado == null).OrderBy(e => e.Id).Take(maximo).ToListAsync(ct);

    public Task<EventoOutbox?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.EventosOutbox.FirstOrDefaultAsync(e => e.Id == id, ct);

    public void Agregar(EventoOutbox evento) => db.EventosOutbox.Add(evento);
}

public sealed class AuditoriaRepositorioEf(LogiMarketDbContext db) : IAuditoriaRepositorio
{
    public Task<bool> ExisteParaEventoAsync(int eventoOrigenId, string categoria, CancellationToken ct = default) =>
        db.Auditorias.AnyAsync(a => a.EventoOrigenId == eventoOrigenId && a.Categoria == categoria, ct);

    public void Agregar(Auditoria auditoria) => db.Auditorias.Add(auditoria);
}

public sealed class UsuarioRepositorioEf(LogiMarketDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct);
}
