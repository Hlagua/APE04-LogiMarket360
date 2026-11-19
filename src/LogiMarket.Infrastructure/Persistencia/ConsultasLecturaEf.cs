using LogiMarket.Application.Consultas;
using LogiMarket.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Lado de consulta (CQRS básico): proyecciones AsNoTracking a DTO, sin SQL concatenado.</summary>
public sealed class ConsultasLecturaEf : IConsultasLectura
{
    private readonly LogiMarketDbContext _db;

    public ConsultasLecturaEf(LogiMarketDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductoDto>> ProductosAsync(string? texto, CancellationToken ct = default)
    {
        var q = _db.Productos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(p => p.Sku.ToLower().Contains(t) || p.Nombre.ToLower().Contains(t) || p.Categoria.ToLower().Contains(t));
        }
        return await q.OrderBy(p => p.Sku)
            .Select(p => new ProductoDto(p.Id, p.Sku, p.Nombre, p.Categoria, p.Descripcion, p.Precio, p.PesoKg, p.Stock, p.Activo))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClienteDto>> ClientesAsync(string? texto, CancellationToken ct = default)
    {
        var q = _db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(t) || c.Identificacion.Contains(t) || c.Email.ToLower().Contains(t));
        }
        var lista = await q.OrderBy(c => c.Nombre).ToListAsync(ct);
        return lista.Select(c => new ClienteDto(c.Id, c.Identificacion, c.Nombre, c.Email, c.Telefono, c.Categoria.ToString(), c.Activo)).ToList();
    }

    public async Task<IReadOnlyList<PedidoResumenDto>> PedidosAsync(CancellationToken ct = default)
    {
        var pedidos = await _db.Pedidos.AsNoTracking()
            .Include(p => p.Lineas).ThenInclude(l => l.Componentes)
            .AsSplitQuery()
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);
        return pedidos.Select(Resumen).ToList();
    }

    private static PedidoResumenDto Resumen(Pedido p) =>
        new(p.Id, p.Numero, p.ClienteId, p.ClienteNombre, p.CategoriaCliente.ToString(), p.Estado.ToString(), p.TipoEntrega.ToString(),
            p.Subtotal, p.Descuento, p.PoliticaDescuento, p.TarifaEnvio, p.Total, p.FechaCreacion, p.FechaEntrega, p.Devuelto,
            p.UnidadesFisicas, p.ImporteReembolsable);

    public async Task<PedidoCompletoDto?> PedidoAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.Pedidos.AsNoTracking()
            .Include(x => x.Lineas).ThenInclude(l => l.Componentes)
            .Include(x => x.Historial)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;

        var pago = await _db.Pagos.AsNoTracking().FirstOrDefaultAsync(x => x.PedidoId == id, ct);
        var despacho = await _db.Despachos.AsNoTracking().FirstOrDefaultAsync(x => x.PedidoId == id, ct);
        var devolucion = await _db.Devoluciones.AsNoTracking().FirstOrDefaultAsync(x => x.PedidoId == id, ct);

        var lineas = p.Lineas.Select(l => new LineaPedidoDto(l.Codigo, l.Descripcion, l.EsKit, l.Cantidad, l.PrecioUnitario,
            l.Subtotal, string.Join(" + ", l.Componentes.Select(c => $"{c.Sku}x{c.CantidadPorUnidad}")))).ToList();
        var historial = p.Historial.OrderBy(h => h.Id)
            .Select(h => new HistorialEstadoDto(h.Fecha, h.EstadoAnterior?.ToString(), h.EstadoNuevo.ToString(), h.Usuario, h.Detalle)).ToList();

        return new PedidoCompletoDto(Resumen(p), p.ClienteEmail, lineas, historial,
            pago is null ? null : new PagoDto(pago.PedidoId, pago.Referencia, pago.ClaveIdempotencia, pago.Monto, pago.Proveedor, pago.CorrelacionId, pago.Fecha),
            despacho is null ? null : new DespachoDto(despacho.PedidoId, despacho.NumeroGuia, despacho.Familia, despacho.PlazoDias,
                despacho.Etiqueta, despacho.FechaDespacho, despacho.FechaEstimadaEntrega, despacho.FechaEntrega, despacho.EstadoSeguimiento),
            devolucion is null ? null : new DevolucionDto(devolucion.PedidoId, devolucion.Motivo.ToString(), devolucion.FechaSolicitud,
                devolucion.DiasDesdeEntrega, devolucion.MontoReembolso, devolucion.ReferenciaReembolso, devolucion.StockReintegrado, devolucion.Observacion));
    }

    public async Task<IReadOnlyList<MovimientoDto>> MovimientosAsync(string? sku, CancellationToken ct = default)
    {
        var q = _db.MovimientosInventario.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sku)) q = q.Where(m => m.Sku == sku.Trim().ToUpper());
        var lista = await q.OrderByDescending(m => m.Id).ToListAsync(ct);
        return lista.Select(m => new MovimientoDto(m.Fecha, m.Sku, m.Cantidad, m.StockResultante, m.Motivo.ToString(), m.Referencia, m.Usuario)).ToList();
    }

    public async Task<IReadOnlyList<AuditoriaDto>> AuditoriaAsync(string? categoria, string? texto, CancellationToken ct = default)
    {
        var q = _db.Auditorias.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(a => a.Categoria == categoria);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(a => a.Detalle.ToLower().Contains(t) || a.Accion.ToLower().Contains(t) || a.Usuario.ToLower().Contains(t));
        }
        return await q.OrderByDescending(a => a.Id).Take(1000)
            .Select(a => new AuditoriaDto(a.Fecha, a.Usuario, a.Categoria, a.Accion, a.Detalle)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxDto>> OutboxAsync(CancellationToken ct = default) =>
        await _db.EventosOutbox.AsNoTracking().OrderByDescending(e => e.Id)
            .Select(e => new OutboxDto(e.Id, e.Tipo, e.PedidoId, e.FechaCreacion, e.FechaProcesado, e.Intentos, e.UltimoError))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PagoDto>> PagosAsync(CancellationToken ct = default) =>
        await _db.Pagos.AsNoTracking().OrderByDescending(p => p.Id)
            .Select(p => new PagoDto(p.PedidoId, p.Referencia, p.ClaveIdempotencia, p.Monto, p.Proveedor, p.CorrelacionId, p.Fecha))
            .ToListAsync(ct);

    public async Task<IndicadoresDto> IndicadoresAsync(int umbralStockBajo, CancellationToken ct = default)
    {
        var estados = await _db.Pedidos.AsNoTracking().Select(p => new { p.Estado, p.Devuelto }).ToListAsync(ct);
        int Contar(EstadoPedido e) => estados.Count(x => x.Estado == e);
        // decimal se guarda como TEXT en SQLite: la suma se hace en memoria para no perder precisión.
        var cobrado = (await _db.Pagos.AsNoTracking().Select(p => p.Monto).ToListAsync(ct)).Sum();
        var reembolsado = (await _db.Devoluciones.AsNoTracking().Select(d => d.MontoReembolso).ToListAsync(ct)).Sum();
        var bajos = await _db.Productos.AsNoTracking().Where(p => p.Activo && p.Stock <= umbralStockBajo)
            .OrderBy(p => p.Stock).Select(p => new { p.Sku, p.Nombre, p.Stock }).ToListAsync(ct);
        var pendientes = await _db.EventosOutbox.CountAsync(e => e.FechaProcesado == null, ct);

        var alertas = bajos.Select(b => $"Stock bajo: {b.Sku} {b.Nombre} ({b.Stock} u.)").ToList();
        if (pendientes > 0) alertas.Add($"Outbox: {pendientes} evento(s) pendiente(s) de procesar.");

        return new IndicadoresDto(Contar(EstadoPedido.BORRADOR), Contar(EstadoPedido.VALIDADO), Contar(EstadoPedido.PAGADO),
            Contar(EstadoPedido.DESPACHADO), Contar(EstadoPedido.ENTREGADO), Contar(EstadoPedido.CANCELADO),
            estados.Count(x => x.Devuelto), cobrado, reembolsado, bajos.Count, pendientes, alertas);
    }
}
