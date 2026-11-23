using LogiMarket.Application.Administracion;
using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Logistica;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Tests.Aplicacion;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Tests.Integracion;

//pruebas de integracion con SQLite real (una base nueva por prueba)
public class FlujosNegocioTests
{
    [Fact(DisplayName = "P01 Base creada desde archivo inexistente; semilla idempotente sin duplicados")]
    public async Task P01_CreacionYSemilla()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        Assert.True(File.Exists(e.RutaBaseDatos));
        using (var scope = e.Proveedor.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IInicializadorBaseDatos>().InicializarAsync(); //relanzar semilla

        var conteos = await e.Db(async db => (
            await db.Productos.CountAsync(), await db.Kits.CountAsync(), await db.Usuarios.CountAsync(),
            await db.Clientes.CountAsync(), (await db.Database.GetAppliedMigrationsAsync()).Count()));
        Assert.Equal((5, 2, 4, 3, 1), conteos);
        var roles = await e.Db(db => db.Usuarios.Select(u => u.Rol.ToString()).ToListAsync());
        Assert.Equal(new[] { "ADMIN", "BODEGA", "CONSULTA", "VENTAS" }, roles.OrderBy(r => r));
        Assert.DoesNotContain(await e.Db(db => db.Usuarios.Select(u => u.HashContrasena).ToListAsync()), h => h.Contains("123!"));
        Assert.Equal((8, 5, 20, 4, 30), (await e.Stock("P01"), await e.Stock("P02"), await e.Stock("P03"), await e.Stock("P04"), await e.Stock("P05")));
    }

    [Fact(DisplayName = "P02 Ana VIP KIT_TELETRABAJO×1 ESTÁNDAR: 59.00; P01=7, P02=4, P03=19; PAGADO y un pago")]
    public async Task P02_Ana()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var r = await e.Confirmar(id);

        Assert.True(r.Exito, r.Mensaje);
        Evidencia.Registrar("P02", "Ana VIP, KIT_TELETRABAJO x1, ESTANDAR, base recién preparada",
            "Total 59.00; P01=7 P02=4 P03=19; PAGADO; 1 pago",
            $"Total {Dinero.Formatear(r.Total)}; P01={await e.Stock("P01")} P02={await e.Stock("P02")} P03={await e.Stock("P03")}; " +
            $"{await e.Db(db => db.Pedidos.Where(p => p.Id == id).Select(p => p.Estado).SingleAsync())}; {await e.Db(db => db.Pagos.CountAsync())} pago");
        Assert.Equal(59.00m, r.Total);
        Assert.Equal((7, 4, 19), (await e.Stock("P01"), await e.Stock("P02"), await e.Stock("P03")));
        var pedido = await e.Db(db => db.Pedidos.SingleAsync(p => p.Id == id));
        Assert.Equal((EstadoPedido.PAGADO, 60.00m, 6.00m, 5.00m), (pedido.Estado, pedido.Subtotal, pedido.Descuento, pedido.TarifaEnvio));
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
        Assert.Equal(3, await e.Db(db => db.MovimientosInventario.CountAsync(m => m.Referencia == pedido.Numero)));
    }

    [Fact(DisplayName = "P03 Luis NORMAL 2 P04 + 2 P05 EXPRÉS: 102.00; P04=2, P05=28")]
    public async Task P03_Luis()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var r = await e.Confirmar(await e.CrearBorrador("Luis", TipoEntrega.EXPRES, ("P04", 2), ("P05", 2)));
        Assert.True(r.Exito, r.Mensaje);
        Evidencia.Registrar("P03", "Luis NORMAL, P04 x2 + P05 x2, EXPRES",
            "Total 102.00; P04=2 P05=28; 1 pago",
            $"Total {Dinero.Formatear(r.Total)}; P04={await e.Stock("P04")} P05={await e.Stock("P05")}; {await e.Db(db => db.Pagos.CountAsync())} pago");
        Assert.Equal(102.00m, r.Total);
        Assert.Equal((2, 28), (await e.Stock("P04"), await e.Stock("P05")));
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
    }

    [Fact(DisplayName = "P04 Carmen MAYORISTA P03×10 ESTÁNDAR: 90.00; descuento 15.00; P03=10")]
    public async Task P04_Carmen()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Carmen", TipoEntrega.ESTANDAR, ("P03", 10));
        var r = await e.Confirmar(id);
        Assert.True(r.Exito, r.Mensaje);
        Evidencia.Registrar("P04", "Carmen MAYORISTA, P03 x10, ESTANDAR",
            "Total 90.00; descuento 15.00; P03=10",
            $"Total {Dinero.Formatear(r.Total)}; descuento {Dinero.Formatear(await e.Db(db => db.Pedidos.Where(p => p.Id == id).Select(p => p.Descuento).SingleAsync()))}; P03={await e.Stock("P03")}");
        Assert.Equal(90.00m, r.Total);
        Assert.Equal(15.00m, await e.Db(db => db.Pedidos.Where(p => p.Id == id).Select(p => p.Descuento).SingleAsync()));
        Assert.Equal(10, await e.Stock("P03"));
    }

    [Fact(DisplayName = "P05 KIT_TELETRABAJO×6: rechazo por P02 insuficiente; sin pago, stock ni evento")]
    public async Task P05_StockInsuficiente()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Luis", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 6));
        var r = await e.Confirmar(id);

        Assert.False(r.Exito);
        Evidencia.Registrar("P05", "KIT_TELETRABAJO x6 con stock inicial",
            "Rechazo STOCK_INSUFICIENTE por P02; 0 pagos; P02=5",
            $"Rechazo {r.Codigo} por {(r.Mensaje.Contains("P02") ? "P02" : "?")}; {await e.Db(db => db.Pagos.CountAsync())} pagos; P02={await e.Stock("P02")}");
        Assert.Equal("STOCK_INSUFICIENTE", r.Codigo);
        Assert.Contains("P02", r.Mensaje);
        await AfirmarSinEfectos(e, id, EstadoPedido.BORRADOR);
    }

    [Fact(DisplayName = "P06 Error de cobro: se conserva inventario y estado; sin pago ni Outbox")]
    public async Task P06_FalloCobro()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        e.Unico<ConfiguracionDemo>().ForzarRechazoCobro = true;
        var rechazo = await e.Confirmar(id);
        Assert.Equal("COBRO_RECHAZADO", rechazo.Codigo);
        await AfirmarSinEfectos(e, id, EstadoPedido.BORRADOR);

        e.Unico<ConfiguracionDemo>().ForzarRechazoCobro = false;
        e.Unico<ConfiguracionDemo>().ForzarErrorPasarela = true;
        var error = await e.Confirmar(id);
        Assert.False(error.Exito);
        await AfirmarSinEfectos(e, id, EstadoPedido.BORRADOR);

        e.Unico<ConfiguracionDemo>().ForzarErrorPasarela = false;
        Assert.True((await e.Confirmar(id)).Exito); //el mismo pedido se puede reintentar despues
    }

    [Fact(DisplayName = "P07 Misma clave de idempotencia: un pago, un movimiento por SKU y resultado original")]
    public async Task P07_Idempotencia()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var primero = await e.Confirmar(id, "CLAVE-ANA");
        var repetido = await e.Confirmar(id, "CLAVE-ANA");

        Assert.True(repetido.Exito);
        Assert.True(repetido.EsRepeticion);
        Evidencia.Registrar("P07", "Confirmar dos veces con la clave CLAVE-ANA",
            "Resultado original; 1 pago; 1 salida de P01",
            $"{(repetido.EsRepeticion && primero.ReferenciaPago == repetido.ReferenciaPago ? "Resultado original" : "Nuevo resultado")}; " +
            $"{await e.Db(db => db.Pagos.CountAsync())} pago; {await e.Db(db => db.MovimientosInventario.CountAsync(m => m.Sku == "P01" && m.Cantidad < 0))} salida de P01");
        Assert.Equal(primero.ReferenciaPago, repetido.ReferenciaPago);
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
        Assert.Equal(1, await e.Db(db => db.MovimientosInventario.CountAsync(m => m.Sku == "P01" && m.Cantidad < 0)));
        Assert.Equal(7, await e.Stock("P01"));
        Assert.Single(e.Unico<IBitacoraTecnica>().Entradas("PAGO"), x => x.Mensaje.StartsWith("INICIO"));

        var otraClave = await e.Confirmar(id, "OTRA-CLAVE"); // el State impide un segundo cobro
        Assert.False(otraClave.Exito);
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
    }

    [Fact(DisplayName = "P08 Pedido PAGADO: cancelar y modificar precio denegados; datos e historial intactos")]
    public async Task P08_PagadoInmutable()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        await e.Confirmar(id);
        var historialAntes = await e.Db(db => db.PedidoHistorial.CountAsync(h => h.PedidoId == id));

        var cancelar = await e.Unico<IInvocadorComandos>().EjecutarAsync(
            new CancelarPedidoCommand(e.Proveedor.CreateScope().ServiceProvider.GetRequiredService<IServicioPedidos>(), id, "prueba"));
        Assert.Equal("TRANSICION_INVALIDA", cancelar.Codigo);

        using (var scope = e.Proveedor.CreateScope())
        {
            var editor = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => scope.ServiceProvider.GetRequiredService<IServicioPedidos>().AbrirEditorAsync(id));
            Assert.Equal("BORRADOR_NO_EDITABLE", editor.Codigo);
            var pedido = await scope.ServiceProvider.GetRequiredService<IPedidoRepositorio>().ObtenerAsync(id);
            Assert.Throws<ExcepcionDominio>(() => pedido!.ActualizarPrecioLinea(CatalogoPrueba.Elemento("KIT_TELETRABAJO")));
        }

        //cambiar el precio actual del catalogo no altera la instantanea del pedido pagado
        await e.IniciarSesion("admin");
        await e.Usar<IServicioCatalogo, ResultadoOperacion>(s =>
            s.ActualizarProductoAsync(new Application.Catalogo.ProductoEdicion("P01", "Mouse USB", "Periféricos", "", 99m, 0.2m, 0)));
        var p = await e.Db(db => db.Pedidos.Include(x => x.Lineas).SingleAsync(x => x.Id == id));
        Assert.Equal((EstadoPedido.PAGADO, 59.00m, 60.00m), (p.Estado, p.Total, p.Lineas.Single().PrecioUnitario));
        Assert.Equal(historialAntes, await e.Db(db => db.PedidoHistorial.CountAsync(h => h.PedidoId == id)));
    }

    [Fact(DisplayName = "P09 BODEGA despacha y entrega; despachar sin pago se rechaza sin cambios")]
    public async Task P09_DespachoEntrega()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var pagado = await e.CrearBorrador("Ana", TipoEntrega.EXPRES, ("KIT_TELETRABAJO", 1));
        await e.Confirmar(pagado);
        var sinPago = await e.CrearBorrador("Luis", TipoEntrega.ESTANDAR, ("P05", 1));

        //VENTAS no puede despachar (autorizacion en Application, no en botones)
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.Usar<IServicioLogistica, ResultadoOperacion<string>>(s => s.DespacharAsync(pagado)));

        await e.IniciarSesion("bodega");
        var invalido = await Assert.ThrowsAsync<ExcepcionDominio>(() => e.Usar<IServicioLogistica, ResultadoOperacion<string>>(s => s.DespacharAsync(sinPago)));
        Assert.Equal("TRANSICION_INVALIDA", invalido.Codigo);
        Assert.Equal(EstadoPedido.BORRADOR, await e.Db(db => db.Pedidos.Where(p => p.Id == sinPago).Select(p => p.Estado).SingleAsync()));
        Assert.Equal(0, await e.Db(db => db.Despachos.CountAsync(d => d.PedidoId == sinPago)));

        var despacho = await e.Usar<IServicioLogistica, ResultadoOperacion<string>>(s => s.DespacharAsync(pagado));
        Assert.StartsWith("EXP-", despacho.Valor);
        await e.Usar<IServicioLogistica, ResultadoOperacion>(s => s.EntregarAsync(pagado));
        Assert.Equal(EstadoPedido.ENTREGADO, await e.Db(db => db.Pedidos.Where(p => p.Id == pagado).Select(p => p.Estado).SingleAsync()));
        Assert.Contains("Entregado", await e.Usar<IServicioConsultas, string>(s => s.SeguimientoAsync(despacho.Valor!)));
    }

    [Fact(DisplayName = "P10 Cerrar y reabrir: pedido, stock, pago y auditoría persisten en SQLite")]
    public async Task P10_Persistencia()
    {
        string ruta;
        int id;
        var carpeta = Path.Combine(Path.GetTempPath(), "logimarket-p10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        ruta = Path.Combine(carpeta, "persistente.db");
        var opciones = new Infrastructure.OpcionesLogiMarket { RutaBaseDatos = ruta, CarpetaSalidas = carpeta };

        //sesion 1
        await using (var sp1 = Infrastructure.RegistroDependencias.AgregarLogiMarket(new ServiceCollection(), opciones).BuildServiceProvider())
        {
            using (var s = sp1.CreateScope()) await s.ServiceProvider.GetRequiredService<IInicializadorBaseDatos>().InicializarAsync();
            using (var s = sp1.CreateScope())
                await s.ServiceProvider.GetRequiredService<IServicioAutenticacion>().IniciarSesionAsync("ventas", "Ventas123!");
            var e1 = sp1;
            using var scope = e1.CreateScope();
            var sp = scope.ServiceProvider;
            var editor = sp.GetRequiredService<IServicioPedidos>().NuevoEditor();
            editor.SeleccionarCliente((await sp.GetRequiredService<IServicioConsultas>().ClientesAsync("Ana")).Single());
            editor.Agregar(await sp.GetRequiredService<IServicioConsultas>().ElementoAsync("KIT_TELETRABAJO"), 1);
            id = (await sp.GetRequiredService<IServicioPedidos>().GuardarBorradorAsync(editor)).Valor;
            using var scope2 = e1.CreateScope();
            Assert.True((await scope2.ServiceProvider.GetRequiredService<IConfirmacionPedido>().ConfirmarAsync(id, "P10")).Exito);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        //sesion 2 (proceso "reabierto")
        await using (var sp2 = Infrastructure.RegistroDependencias.AgregarLogiMarket(new ServiceCollection(), opciones).BuildServiceProvider())
        {
            using (var s = sp2.CreateScope()) await s.ServiceProvider.GetRequiredService<IInicializadorBaseDatos>().InicializarAsync();
            using var scope = sp2.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistencia.LogiMarketDbContext>();
            var pedido = await db.Pedidos.SingleAsync(p => p.Id == id);
            Assert.Equal((EstadoPedido.PAGADO, 59.00m), (pedido.Estado, pedido.Total));
            Assert.Equal(7, await db.Productos.Where(p => p.Sku == "P01").Select(p => p.Stock).SingleAsync());
            Assert.Equal("P10", (await db.Pagos.SingleAsync()).ClaveIdempotencia);
            Assert.True(await db.Auditorias.AnyAsync(a => a.Accion == "PEDIDO_CONFIRMADO"));
            Assert.Equal(5, await db.Productos.CountAsync()); //la semilla no duplico
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(carpeta, true); } catch (IOException) { }
    }

    [Fact(DisplayName = "P20 Facade: fallo de un colaborador no deja persistencia parcial")]
    public async Task P20_FacadeConsistente()
    {
        var espia = new PasarelaEspia { Lanzar = true };
        await using var e = await EntornoPrueba.CrearAsync(s => s.AddScoped<IPasarelaPago>(_ => espia));
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var r = await e.Confirmar(id);
        Assert.Equal("ERROR_COLABORADOR", r.Codigo);
        Assert.Equal(1, espia.Invocaciones);
        await AfirmarSinEfectos(e, id, EstadoPedido.BORRADOR);
        Assert.True(await e.Db(db => db.Auditorias.AnyAsync(a => a.Accion == "CONFIRMACION_RECHAZADA")));
    }

    [Fact(DisplayName = "P24 Command: la misma confirmación desde el invocador registra comando, entrada y resultado")]
    public async Task P24_Command()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Luis", TipoEntrega.EXPRES, ("P04", 2), ("P05", 2));
        var invocador = e.Unico<IInvocadorComandos>();
        using var scope = e.Proveedor.CreateScope();
        var comando = new ConfirmarPedidoCommand(scope.ServiceProvider.GetRequiredService<IConfirmacionPedido>(), id,
            await e.ClaveDefecto(id));

        var r1 = await invocador.EjecutarAsync(comando); //"boton"
        var r2 = await invocador.EjecutarAsync(comando); //reintento de "prueba"
        Assert.True(r1.Exito);
        Assert.Equal("YA_CONFIRMADO", r2.Codigo);
        Assert.Equal(2, invocador.Historial.Count);
        Assert.All(invocador.Historial, h => Assert.Equal(nameof(ConfirmarPedidoCommand), h.Comando));
        Assert.Contains($"pedido={id}", invocador.Historial[0].Entrada);
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
    }

    [Fact(DisplayName = "P30 Observer: PedidoPagado actualiza notificación, auditoría y métrica una sola vez")]
    public async Task P30_Observer()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var metricas = e.Unico<IMetricasSesion>();
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        await e.Confirmar(id);

        Assert.Equal(1, metricas.Instantanea().Notificaciones);
        Assert.Equal(1, metricas.Instantanea().EventosProcesados);
        Assert.Equal(1, await e.Db(db => db.Auditorias.CountAsync(a => a.Categoria == ObservadorAuditoria.Categoria && a.Accion == "PedidoPagado")));
        Assert.Contains("ana@ejemplo.test", File.ReadAllText(Path.Combine(e.CarpetaSalidas, "notificaciones.log")));

        //reprocesar no duplica (el evento ya esta marcado)
        var otra = await e.Usar<IProcesadorOutbox, ResultadoProcesoOutbox>(p => p.ProcesarPendientesAsync());
        Assert.Equal(0, otra.Procesados);
        Assert.Equal(1, metricas.Instantanea().Notificaciones);
        Assert.Equal(new[] { "Notificación", "Auditoría", "Métricas" }, e.Proveedor.CreateScope().ServiceProvider.GetRequiredService<IPublicadorEventos>().Suscriptores);
    }

    [Fact(DisplayName = "P33 Template Method: defecto día 20 aceptado; arrepentimiento día 10 rechazado sin efectos")]
    public async Task P33_TemplateMethod()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        var arrepentimiento = await PedidoEntregado(e, "Luis", ("P04", 1));
        var defecto = await PedidoEntregado(e, "Ana", ("KIT_TELETRABAJO", 1));
        await e.IniciarSesion("ventas");

        e.Reloj.AvanzarDias(10);
        var r1 = await Devolver(e, arrepentimiento, MotivoDevolucion.ARREPENTIMIENTO);
        Assert.Equal("FUERA_DE_PLAZO", r1.Codigo);
        Assert.Equal(3, await e.Stock("P04"));
        Assert.Equal(0, await e.Db(db => db.Devoluciones.CountAsync()));

        e.Reloj.AvanzarDias(10); //dia 20
        var r2 = await Devolver(e, defecto, MotivoDevolucion.DEFECTO);
        Assert.True(r2.Exito, r2.Mensaje);
        Assert.Equal(20, r2.DiasDesdeEntrega);
        Assert.Equal(new[] { "1-VerificarElegibilidad", "2-InspeccionarCondicion", "3-CalcularReembolso", "4-RegistrarDevolucionYStock", "5-GenerarEvento" }, r2.Pasos);
    }

    [Fact(DisplayName = "Template Method: arrepentimiento en plazo pero con producto usado se rechaza")]
    public async Task TemplateMethod_ArrepentimientoUsado()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        var id = await PedidoEntregado(e, "Luis", ("P05", 2));
        await e.IniciarSesion("ventas");
        e.Reloj.AvanzarDias(3);
        var usado = await Devolver(e, id, MotivoDevolucion.ARREPENTIMIENTO, sinUso: false);
        Assert.Equal("CONDICION_NO_ACEPTADA", usado.Codigo);
        var ok = await Devolver(e, id, MotivoDevolucion.ARREPENTIMIENTO, sinUso: true);
        Assert.True(ok.Exito, ok.Mensaje);
        Assert.Equal(10.00m, ok.MontoReembolso);
    }

    [Fact(DisplayName = "P35 Devolución de Ana por defecto: 54.00 sin flete; reintegro y devolución únicos")]
    public async Task P35_DevolucionAna()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        var id = await PedidoEntregado(e, "Ana", ("KIT_TELETRABAJO", 1));
        await e.IniciarSesion("ventas");
        e.Reloj.AvanzarDias(5);

        var r = await Devolver(e, id, MotivoDevolucion.DEFECTO);
        var repetida = await Devolver(e, id, MotivoDevolucion.DEFECTO);

        Assert.True(r.Exito, r.Mensaje);
        Evidencia.Registrar("P35", "Ana entregada, devolución DEFECTO día 5 y repetición",
            "Reembolso 54.00; repetición DEVOLUCION_DUPLICADA; P01=8 P02=5 P03=20; 1 devolución",
            $"Reembolso {Dinero.Formatear(r.MontoReembolso)}; repetición {repetida.Codigo}; P01={await e.Stock("P01")} P02={await e.Stock("P02")} P03={await e.Stock("P03")}; " +
            $"{await e.Db(db => db.Devoluciones.CountAsync())} devolución");
        Assert.Equal(54.00m, r.MontoReembolso);
        Assert.Equal("DEVOLUCION_DUPLICADA", repetida.Codigo);
        Assert.Equal((8, 5, 20), (await e.Stock("P01"), await e.Stock("P02"), await e.Stock("P03")));
        Assert.Equal(1, await e.Db(db => db.Devoluciones.CountAsync()));
        Assert.Equal(3, await e.Db(db => db.MovimientosInventario.CountAsync(m => m.Motivo == Domain.Inventario.MotivoMovimiento.DEVOLUCION)));
    }

    [Fact(DisplayName = "P36 Dos cobros concurrentes sobre un SKU con stock 1: máximo uno aprobado, sin sobreventa")]
    public async Task P36_Concurrencia()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("admin");
        await e.Usar<IServicioCatalogo, ResultadoOperacion>(s => s.AjustarStockAsync("P04", 1, "Preparación P36"));
        await e.IniciarSesion("ventas");
        var a = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("P04", 1));
        var b = await e.CrearBorrador("Luis", TipoEntrega.ESTANDAR, ("P04", 1));

        var resultados = await Task.WhenAll(Task.Run(() => e.Confirmar(a)), Task.Run(() => e.Confirmar(b)));

        Evidencia.Registrar("P36", "Dos cobros simultáneos de P04 con stock 1",
            "1 aprobado; stock P04=0; 1 pago",
            $"{resultados.Count(r => r.Exito)} aprobado; stock P04={await e.Stock("P04")}; {await e.Db(db => db.Pagos.CountAsync())} pago");
        Assert.Equal(1, resultados.Count(r => r.Exito));
        Assert.Contains(resultados, r => !r.Exito && r.Codigo is "STOCK_INSUFICIENTE" or "CONFLICTO_CONCURRENCIA");
        Assert.Equal(0, await e.Stock("P04"));
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync()));
    }

    [Fact(DisplayName = "P37 Outbox: falla el notificador, el evento queda pendiente y se recupera una sola vez")]
    public async Task P37_OutboxRecuperacion()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("ventas");
        var demo = e.Unico<ConfiguracionDemo>();
        demo.ForzarFalloNotificador = true;
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var r = await e.Confirmar(id);
        Assert.True(r.Exito); //el negocio se confirmo aunque el aviso falle

        var pendiente = await e.Db(db => db.EventosOutbox.SingleAsync());
        Assert.Null(pendiente.FechaProcesado);
        Assert.Equal(1, pendiente.Intentos);
        Assert.Contains("fallo simulado", pendiente.UltimoError);
        Assert.Equal(0, await e.Db(db => db.Auditorias.CountAsync(a => a.Categoria == ObservadorAuditoria.Categoria)));

        demo.ForzarFalloNotificador = false;
        var recuperado = await e.Usar<IProcesadorOutbox, ResultadoProcesoOutbox>(p => p.ProcesarPendientesAsync()); //procesador "reiniciado"
        var otraVez = await e.Usar<IProcesadorOutbox, ResultadoProcesoOutbox>(p => p.ProcesarPendientesAsync());

        Assert.Equal(1, recuperado.Procesados);
        Assert.Equal(0, otraVez.Procesados);
        var evento = await e.Db(db => db.EventosOutbox.SingleAsync());
        Assert.NotNull(evento.FechaProcesado);
        Assert.Equal(2, evento.Intentos);
        Assert.Equal(1, await e.Db(db => db.Auditorias.CountAsync(a => a.Categoria == ObservadorAuditoria.Categoria)));
        Assert.Equal(1, await e.Db(db => db.Pagos.CountAsync())); //sin cobro duplicado
    }

    [Fact(DisplayName = "P38 LSP: el flujo completo funciona igual con el proveedor LEGADO y el MODERNO")]
    public async Task P38_LspProveedores()
    {
        foreach (var proveedor in new[] { ConfiguracionDemo.ProveedorLegado, ConfiguracionDemo.ProveedorModerno })
        {
            await using var e = await EntornoPrueba.CrearAsync();
            e.Unico<ConfiguracionDemo>().ProveedorPago = proveedor;
            await e.IniciarSesion("ventas");
            var r = await e.Confirmar(await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1)));
            Assert.True(r.Exito, r.Mensaje);
            Assert.Equal(59.00m, r.Total);
            Assert.Equal(proveedor, await e.Db(db => db.Pagos.Select(p => p.Proveedor).SingleAsync()));
        }
    }

    [Fact(DisplayName = "Seguridad: CONSULTA no puede crear borradores ni confirmar; login inválido rechazado")]
    public async Task Seguridad_Permisos()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        var malo = await e.Usar<IServicioAutenticacion, ResultadoOperacion<Application.Seguridad.UsuarioSesion>>(
            s => s.IniciarSesionAsync("ventas", "incorrecta"));
        Assert.Equal("CREDENCIALES_INVALIDAS", malo.Codigo);

        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("P01", 1));
        await e.IniciarSesion("consulta");
        Assert.Throws<AccesoDenegadoException>(() => e.Proveedor.CreateScope().ServiceProvider.GetRequiredService<IServicioPedidos>().NuevoEditor());
        var r = await e.Confirmar(id);
        Assert.Equal("ACCESO_DENEGADO", r.Codigo);
        Assert.Equal(2, e.Unico<IMetricasSesion>().Instantanea().OperacionesDenegadas); //NuevoEditor + Confirmar, sin duplicar
        Assert.Empty(e.Unico<IBitacoraTecnica>().Entradas("PAGO"));
        Assert.Equal(8, await e.Stock("P01"));
    }

    [Fact(DisplayName = "Catálogo: crear kit anidado y rechazar ciclo indirecto al persistir")]
    public async Task Catalogo_KitCicloPersistido()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("admin");
        var crear = await e.Usar<IServicioCatalogo, ResultadoOperacion>(s =>
            s.CrearKitAsync("KIT_GAMER", "Kit Gamer", new[] { ("KIT_TELETRABAJO", 1), ("P04", 1) }));
        Assert.True(crear.Exito, crear.Mensaje);
        var kits = await e.Usar<IServicioConsultas, IReadOnlyList<Application.Catalogo.KitDto>>(s => s.KitsAsync());
        Assert.Equal(100.00m, kits.Single(k => k.Codigo == "KIT_GAMER").Precio);

        var ciclo = await Assert.ThrowsAsync<ExcepcionDominio>(() => e.Usar<IServicioCatalogo, ResultadoOperacion>(s =>
            s.AgregarComponenteAsync("KIT_OFICINA", "KIT_GAMER", 1)));
        Assert.Equal("KIT_CICLO", ciclo.Codigo);
        Assert.Equal(2, await e.Db(db => db.Kits.Where(k => k.Codigo == "KIT_OFICINA").SelectMany(k => k.Componentes).CountAsync()));
    }

    //ayudas

    private static async Task AfirmarSinEfectos(EntornoPrueba e, int pedidoId, EstadoPedido estadoEsperado)
    {
        Assert.Equal(estadoEsperado, await e.Db(db => db.Pedidos.Where(p => p.Id == pedidoId).Select(p => p.Estado).SingleAsync()));
        Assert.Equal(0, await e.Db(db => db.Pagos.CountAsync(p => p.PedidoId == pedidoId)));
        Assert.Equal(0, await e.Db(db => db.EventosOutbox.CountAsync(o => o.PedidoId == pedidoId)));
        Assert.Equal(0, await e.Db(db => db.MovimientosInventario.CountAsync(m => m.Motivo == Domain.Inventario.MotivoMovimiento.VENTA)));
        Assert.Equal((8, 5, 20, 4, 30), (await e.Stock("P01"), await e.Stock("P02"), await e.Stock("P03"), await e.Stock("P04"), await e.Stock("P05")));
    }

    private static async Task<int> PedidoEntregado(EntornoPrueba e, string cliente, params (string, int)[] lineas)
    {
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador(cliente, TipoEntrega.ESTANDAR, lineas);
        Assert.True((await e.Confirmar(id)).Exito);
        await e.IniciarSesion("bodega");
        await e.Usar<IServicioLogistica, ResultadoOperacion<string>>(s => s.DespacharAsync(id));
        await e.Usar<IServicioLogistica, ResultadoOperacion>(s => s.EntregarAsync(id));
        return id;
    }

    private static Task<ResultadoDevolucion> Devolver(EntornoPrueba e, int id, MotivoDevolucion motivo, bool sinUso = true) =>
        e.Usar<IServicioDevoluciones, ResultadoDevolucion>(s => s.SolicitarAsync(new SolicitudDevolucion(id, motivo, sinUso, "prueba")));
}
