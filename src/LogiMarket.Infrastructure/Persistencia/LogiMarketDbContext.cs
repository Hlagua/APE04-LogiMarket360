using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Inventario;
using LogiMarket.Domain.Logistica;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>DbContext de EF Core sobre SQLite. Es la base de la Unidad de Trabajo.</summary>
public sealed class LogiMarketDbContext : DbContext
{
    public LogiMarketDbContext(DbContextOptions<LogiMarketDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Kit> Kits => Set<Kit>();
    public DbSet<KitComponente> KitComponentes => Set<KitComponente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();
    public DbSet<PedidoDetalleComponente> PedidoDetalleComponentes => Set<PedidoDetalleComponente>();
    public DbSet<PedidoHistorialEstado> PedidoHistorial => Set<PedidoHistorialEstado>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<Despacho> Despachos => Set<Despacho>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<EventoOutbox> EventosOutbox => Set<EventoOutbox>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Usuario>(e =>
        {
            e.ToTable("Usuario");
            e.HasKey(x => x.Id);
            e.Property(x => x.NombreUsuario).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.NombreUsuario).IsUnique();
            e.Property(x => x.NombreCompleto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HashContrasena).HasMaxLength(300).IsRequired();
        });

        mb.Entity<Cliente>(e =>
        {
            e.ToTable("Cliente");
            e.HasKey(x => x.Id);
            e.Property(x => x.Identificacion).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Identificacion).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Nombre);
            e.Property(x => x.Email).HasMaxLength(120).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
        });

        mb.Entity<Producto>(e =>
        {
            e.ToTable("Producto", t =>
            {
                t.HasCheckConstraint("CK_Producto_Precio", "CAST(Precio AS REAL) > 0");
                t.HasCheckConstraint("CK_Producto_Stock", "Stock >= 0");
                t.HasCheckConstraint("CK_Producto_Peso", "CAST(PesoKg AS REAL) >= 0");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Nombre);
            e.Property(x => x.Categoria).HasMaxLength(60).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            Moneda(e.Property(x => x.Precio));
            e.Property(x => x.PesoKg).HasPrecision(10, 3);
            e.Property(x => x.Version).IsConcurrencyToken(); // concurrencia optimista contra sobreventa
        });

        mb.Entity<Kit>(e =>
        {
            e.ToTable("Kit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            e.HasMany(x => x.Componentes).WithOne().HasForeignKey(c => c.KitId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Componentes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        mb.Entity<KitComponente>(e =>
        {
            e.ToTable("KitComponente", t => t.HasCheckConstraint("CK_KitComponente_Referencia",
                "(ProductoId IS NULL AND KitHijoId IS NOT NULL) OR (ProductoId IS NOT NULL AND KitHijoId IS NULL)"));
            e.HasKey(x => x.Id);
            e.Property(x => x.CodigoComponente).HasMaxLength(30).IsRequired();
            e.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Kit>().WithMany().HasForeignKey(x => x.KitHijoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.KitId, x.CodigoComponente }).IsUnique();
            e.Ignore(x => x.EsKit);
        });

        mb.Entity<Pedido>(e =>
        {
            e.ToTable("Pedido");
            e.HasKey(x => x.Id);
            e.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ClienteNombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.ClienteEmail).HasMaxLength(120);
            e.Property(x => x.ClienteTelefono).HasMaxLength(30);
            e.Property(x => x.CategoriaCliente).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TipoEntrega).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Estado);
            Moneda(e.Property(x => x.Subtotal));
            Moneda(e.Property(x => x.Descuento));
            Moneda(e.Property(x => x.TarifaEnvio));
            Moneda(e.Property(x => x.Total));
            e.Property(x => x.PoliticaDescuento).HasMaxLength(30);
            e.Property(x => x.ClaveIdempotencia).HasMaxLength(100);
            e.HasIndex(x => x.ClaveIdempotencia).IsUnique();
            e.Property(x => x.CreadoPor).HasMaxLength(50);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasMany(x => x.Lineas).WithOne().HasForeignKey(l => l.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.Historial).WithOne().HasForeignKey(h => h.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Historial).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Ignore(x => x.Numero);
            e.Ignore(x => x.UnidadesFisicas);
            e.Ignore(x => x.PesoKg);
            e.Ignore(x => x.SubtotalCalculado);
            e.Ignore(x => x.ImporteReembolsable);
            e.Ignore(x => x.PermiteEdicion);
        });

        mb.Entity<PedidoDetalle>(e =>
        {
            e.ToTable("PedidoDetalle");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(120);
            Moneda(e.Property(x => x.PrecioUnitario));
            e.HasMany(x => x.Componentes).WithOne().HasForeignKey(c => c.PedidoDetalleId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Componentes).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Ignore(x => x.Subtotal);
            e.Ignore(x => x.UnidadesFisicas);
            e.Ignore(x => x.PesoKg);
        });

        mb.Entity<PedidoDetalleComponente>(e =>
        {
            e.ToTable("PedidoDetalleComponente");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(30).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(120);
            Moneda(e.Property(x => x.PrecioUnitario));
            e.Property(x => x.PesoKg).HasPrecision(10, 3);
        });

        mb.Entity<PedidoHistorialEstado>(e =>
        {
            e.ToTable("PedidoHistorialEstado");
            e.HasKey(x => x.Id);
            e.Property(x => x.EstadoAnterior).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EstadoNuevo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Usuario).HasMaxLength(50);
            e.Property(x => x.Detalle).HasMaxLength(300);
        });

        mb.Entity<Pago>(e =>
        {
            e.ToTable("Pago");
            e.HasKey(x => x.Id);
            e.HasOne<Pedido>().WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PedidoId).IsUnique();          // un solo cobro por pedido
            e.Property(x => x.Referencia).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Referencia).IsUnique();
            e.Property(x => x.ClaveIdempotencia).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.ClaveIdempotencia).IsUnique(); // idempotencia
            Moneda(e.Property(x => x.Monto));
            e.Property(x => x.Proveedor).HasMaxLength(50);
            e.Property(x => x.CorrelacionId).HasMaxLength(50);
        });

        mb.Entity<MovimientoInventario>(e =>
        {
            e.ToTable("MovimientoInventario");
            e.HasKey(x => x.Id);
            e.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Sku).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Sku);
            e.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Referencia).HasMaxLength(120);
            e.Property(x => x.Usuario).HasMaxLength(50);
        });

        mb.Entity<Despacho>(e =>
        {
            e.ToTable("Despacho");
            e.HasKey(x => x.Id);
            e.HasOne<Pedido>().WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PedidoId).IsUnique();
            e.Property(x => x.NumeroGuia).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.NumeroGuia).IsUnique();
            e.Property(x => x.Familia).HasMaxLength(20);
            e.Property(x => x.Etiqueta).HasMaxLength(400);
            e.Property(x => x.Usuario).HasMaxLength(50);
            e.Ignore(x => x.EstadoSeguimiento);
        });

        mb.Entity<Devolucion>(e =>
        {
            e.ToTable("Devolucion");
            e.HasKey(x => x.Id);
            e.HasOne<Pedido>().WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PedidoId).IsUnique();           // devolución única por pedido
            e.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(20);
            Moneda(e.Property(x => x.MontoReembolso));
            e.Property(x => x.ReferenciaReembolso).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.ReferenciaReembolso).IsUnique(); // reembolso único
            e.Property(x => x.Observacion).HasMaxLength(300);
            e.Property(x => x.Usuario).HasMaxLength(50);
        });

        mb.Entity<EventoOutbox>(e =>
        {
            e.ToTable("EventoOutbox");
            e.HasKey(x => x.Id);
            e.HasOne<Pedido>().WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Tipo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.UltimoError).HasMaxLength(500);
            e.HasIndex(x => x.FechaProcesado);
            e.Ignore(x => x.Procesado);
        });

        mb.Entity<Auditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Usuario).HasMaxLength(50);
            e.Property(x => x.Categoria).HasMaxLength(30);
            e.HasIndex(x => x.Categoria);
            e.Property(x => x.Accion).HasMaxLength(60);
            e.Property(x => x.Detalle).HasMaxLength(1000);
            e.HasIndex(x => new { x.EventoOrigenId, x.Categoria });
        });
    }

    private static void Moneda(PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);
}
