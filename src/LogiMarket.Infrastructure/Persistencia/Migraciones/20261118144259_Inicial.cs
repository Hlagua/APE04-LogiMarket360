using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiMarket.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Accion = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EventoOrigenId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identificacion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Precio = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PesoKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.Id);
                    table.CheckConstraint("CK_Producto_Peso", "CAST(PesoKg AS REAL) >= 0");
                    table.CheckConstraint("CK_Producto_Precio", "CAST(Precio AS REAL) > 0");
                    table.CheckConstraint("CK_Producto_Stock", "Stock >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HashContrasena = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClienteNombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ClienteEmail = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ClienteTelefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CategoriaCliente = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TipoEntrega = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Descuento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PoliticaDescuento = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TarifaEnvio = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaEntrega = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClaveIdempotencia = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Devuelto = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreadoPor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedido_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KitId = table.Column<int>(type: "INTEGER", nullable: false),
                    CodigoComponente = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: true),
                    KitHijoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitComponente", x => x.Id);
                    table.CheckConstraint("CK_KitComponente_Referencia", "(ProductoId IS NULL AND KitHijoId IS NOT NULL) OR (ProductoId IS NOT NULL AND KitHijoId IS NULL)");
                    table.ForeignKey(
                        name: "FK_KitComponente_Kit_KitHijoId",
                        column: x => x.KitHijoId,
                        principalTable: "Kit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitComponente_Kit_KitId",
                        column: x => x.KitId,
                        principalTable: "Kit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitComponente_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    StockResultante = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientoInventario_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Despacho",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroGuia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Familia = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PlazoDias = table.Column<int>(type: "INTEGER", nullable: false),
                    Etiqueta = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    FechaDespacho = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEstimadaEntrega = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEntrega = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despacho", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Despacho_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Devolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DiasDesdeEntrega = table.Column<int>(type: "INTEGER", nullable: false),
                    MontoReembolso = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ReferenciaReembolso = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StockReintegrado = table.Column<bool>(type: "INTEGER", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devolucion_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventoOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaProcesado = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Intentos = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventoOutbox_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Monto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Proveedor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CorrelacionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pago_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PedidoDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EsKit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoDetalle_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PedidoHistorialEstado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoHistorialEstado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoHistorialEstado_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PedidoDetalleComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoDetalleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CantidadPorUnidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PesoKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoDetalleComponente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoDetalleComponente_PedidoDetalle_PedidoDetalleId",
                        column: x => x.PedidoDetalleId,
                        principalTable: "PedidoDetalle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Categoria",
                table: "Auditoria",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_EventoOrigenId_Categoria",
                table: "Auditoria",
                columns: new[] { "EventoOrigenId", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Identificacion",
                table: "Cliente",
                column: "Identificacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Nombre",
                table: "Cliente",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Despacho_NumeroGuia",
                table: "Despacho",
                column: "NumeroGuia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Despacho_PedidoId",
                table: "Despacho",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devolucion_PedidoId",
                table: "Devolucion",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devolucion_ReferenciaReembolso",
                table: "Devolucion",
                column: "ReferenciaReembolso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventoOutbox_FechaProcesado",
                table: "EventoOutbox",
                column: "FechaProcesado");

            migrationBuilder.CreateIndex(
                name: "IX_EventoOutbox_PedidoId",
                table: "EventoOutbox",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Kit_Codigo",
                table: "Kit",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_KitHijoId",
                table: "KitComponente",
                column: "KitHijoId");

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_KitId_CodigoComponente",
                table: "KitComponente",
                columns: new[] { "KitId", "CodigoComponente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_ProductoId",
                table: "KitComponente",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_ProductoId",
                table: "MovimientoInventario",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_Sku",
                table: "MovimientoInventario",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Pago_ClaveIdempotencia",
                table: "Pago",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pago_PedidoId",
                table: "Pago",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pago_Referencia",
                table: "Pago",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_ClaveIdempotencia",
                table: "Pedido",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_ClienteId",
                table: "Pedido",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_Estado",
                table: "Pedido",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalle_PedidoId",
                table: "PedidoDetalle",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalleComponente_PedidoDetalleId",
                table: "PedidoDetalleComponente",
                column: "PedidoDetalleId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoHistorialEstado_PedidoId",
                table: "PedidoHistorialEstado",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Nombre",
                table: "Producto",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Sku",
                table: "Producto",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_NombreUsuario",
                table: "Usuario",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Despacho");

            migrationBuilder.DropTable(
                name: "Devolucion");

            migrationBuilder.DropTable(
                name: "EventoOutbox");

            migrationBuilder.DropTable(
                name: "KitComponente");

            migrationBuilder.DropTable(
                name: "MovimientoInventario");

            migrationBuilder.DropTable(
                name: "Pago");

            migrationBuilder.DropTable(
                name: "PedidoDetalleComponente");

            migrationBuilder.DropTable(
                name: "PedidoHistorialEstado");

            migrationBuilder.DropTable(
                name: "Usuario");

            migrationBuilder.DropTable(
                name: "Kit");

            migrationBuilder.DropTable(
                name: "Producto");

            migrationBuilder.DropTable(
                name: "PedidoDetalle");

            migrationBuilder.DropTable(
                name: "Pedido");

            migrationBuilder.DropTable(
                name: "Cliente");
        }
    }
}
