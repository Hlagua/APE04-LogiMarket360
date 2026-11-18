using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>
/// Aplica las migraciones EF Core (creando la base desde cero si no existe) y ejecuta una semilla
/// IDEMPOTENTE: cada registro se busca por su clave natural (SKU, código, usuario, identificación)
/// antes de insertarlo, por lo que relanzarla no duplica datos.
/// </summary>
public sealed class InicializadorBaseDatos : IInicializadorBaseDatos
{
    private readonly LogiMarketDbContext _db;
    private readonly IHasherContrasena _hasher;
    private readonly IBitacoraTecnica _bitacora;

    public InicializadorBaseDatos(LogiMarketDbContext db, IHasherContrasena hasher, IBitacoraTecnica bitacora)
    {
        _db = db;
        _hasher = hasher;
        _bitacora = bitacora;
    }

    public string Descripcion => _db.Database.GetDbConnection().DataSource;

    /// <summary>Catálogo oficial de la guía APE 04.</summary>
    public static readonly (string Sku, string Nombre, string Categoria, string Descripcion, decimal Precio, int Stock, decimal Peso)[] ProductosOficiales =
    {
        ("P01", "Mouse USB", "Periféricos", "Mouse óptico con cable USB", 20.00m, 8, 0.20m),
        ("P02", "Teclado", "Periféricos", "Teclado estándar en español", 30.00m, 5, 0.70m),
        ("P03", "Memoria USB", "Almacenamiento", "Memoria flash USB", 10.00m, 20, 0.10m),
        ("P04", "Webcam", "Video", "Cámara web HD", 40.00m, 4, 0.35m),
        ("P05", "Alfombrilla", "Accesorios", "Alfombrilla para mouse", 5.00m, 30, 0.10m)
    };

    /// <summary>Cuentas de DEMOSTRACIÓN (ficticias, documentadas en el README). Solo se guarda su hash.</summary>
    public static readonly (string Usuario, string Nombre, Rol Rol, string Clave)[] UsuariosDemo =
    {
        ("admin", "Administrador Demo", Rol.ADMIN, "Admin123!"),
        ("ventas", "Vendedor Demo", Rol.VENTAS, "Ventas123!"),
        ("bodega", "Bodeguero Demo", Rol.BODEGA, "Bodega123!"),
        ("consulta", "Consulta Demo", Rol.CONSULTA, "Consulta123!")
    };

    public static readonly (string Id, string Nombre, string Email, string Telefono, CategoriaCliente Categoria)[] ClientesDemo =
    {
        ("0000000001", "Ana", "ana@ejemplo.test", "0990000001", CategoriaCliente.VIP),
        ("0000000002", "Luis", "luis@ejemplo.test", "0990000002", CategoriaCliente.NORMAL),
        ("0000000003", "Carmen", "carmen@ejemplo.test", "0990000003", CategoriaCliente.MAYORISTA)
    };

    public async Task InicializarAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
        await SembrarAsync(ct);
    }

    public async Task RestablecerAsync(CancellationToken ct = default)
    {
        await _db.Database.EnsureDeletedAsync(ct);
        _db.ChangeTracker.Clear();
        await InicializarAsync(ct);
        _bitacora.Registrar("SEMILLA", "Base de pruebas restablecida a los datos controlados.");
    }

    private async Task SembrarAsync(CancellationToken ct)
    {
        var ahora = DateTime.Now;
        var insertados = 0;

        foreach (var p in ProductosOficiales)
        {
            if (await _db.Productos.AnyAsync(x => x.Sku == p.Sku, ct)) continue;
            var producto = Producto.Crear(p.Sku, p.Nombre, p.Categoria, p.Descripcion, p.Precio, p.Peso, p.Stock);
            _db.Productos.Add(producto);
            await _db.SaveChangesAsync(ct);
            _db.MovimientosInventario.Add(new MovimientoInventario(producto.Id, producto.Sku, p.Stock, p.Stock,
                MotivoMovimiento.INICIAL, "Semilla", ahora, "sistema"));
            insertados++;
        }
        await _db.SaveChangesAsync(ct);

        // Orden obligatorio: KIT_OFICINA existe antes de KIT_TELETRABAJO (que lo contiene).
        if (!await _db.Kits.AnyAsync(k => k.Codigo == "KIT_OFICINA", ct))
        {
            var oficina = Kit.Crear("KIT_OFICINA", "Kit Oficina");
            oficina.AgregarComponente(await ProductoPorSku("P01", ct), 1);
            oficina.AgregarComponente(await ProductoPorSku("P02", ct), 1);
            _db.Kits.Add(oficina);
            await _db.SaveChangesAsync(ct);
            insertados++;
        }
        if (!await _db.Kits.AnyAsync(k => k.Codigo == "KIT_TELETRABAJO", ct))
        {
            var teletrabajo = Kit.Crear("KIT_TELETRABAJO", "Kit Teletrabajo");
            teletrabajo.AgregarComponente(await _db.Kits.SingleAsync(k => k.Codigo == "KIT_OFICINA", ct), 1);
            teletrabajo.AgregarComponente(await ProductoPorSku("P03", ct), 1);
            _db.Kits.Add(teletrabajo);
            await _db.SaveChangesAsync(ct);
            insertados++;
        }

        foreach (var u in UsuariosDemo)
        {
            if (await _db.Usuarios.AnyAsync(x => x.NombreUsuario == u.Usuario, ct)) continue;
            _db.Usuarios.Add(Usuario.Crear(u.Usuario, u.Nombre, u.Rol, _hasher.Hashear(u.Clave)));
            insertados++;
        }

        foreach (var c in ClientesDemo)
        {
            if (await _db.Clientes.AnyAsync(x => x.Identificacion == c.Id, ct)) continue;
            _db.Clientes.Add(Cliente.Crear(c.Id, c.Nombre, c.Email, c.Telefono, c.Categoria));
            insertados++;
        }
        await _db.SaveChangesAsync(ct);
        _bitacora.Registrar("SEMILLA", $"Semilla idempotente ejecutada: {insertados} registro(s) nuevo(s).");
    }

    private Task<Producto> ProductoPorSku(string sku, CancellationToken ct) => _db.Productos.SingleAsync(p => p.Sku == sku, ct);
}
