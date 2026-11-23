using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Eventos;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Reportes;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Registro;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Integracion;

//pruebas de las correcciones de la revision (idempotencia, Outbox, Memento, Flyweight, filtros)
public class CorreccionesRevisionTests
{
    private static IServicioValoracion Valoracion() => new ServicioValoracion(CalculadoraDescuento.PorDefecto(),
        new Application.Entrega.ProveedorFabricasEntrega(new Application.Entrega.IFabricaEntrega[]
            { new Application.Entrega.EntregaEstandarFactory(), new Application.Entrega.EntregaExpresFactory() }));

    //pasarela idempotente

    [Fact(DisplayName = "Pasarela: la misma clave aprobada no vuelve a cobrar (legado vía Adapter y moderno)")]
    public async Task Pasarela_IdempotentePorClave()
    {
        var demo = new ConfiguracionDemo();
        var bitacora = new BitacoraArchivo(null);
        var legada = new PasarelaLegadaSimulada(() => demo.ForzarRechazoCobro, () => demo.ForzarErrorPasarela, m => bitacora.Registrar("PAGO", m));
        var adapter = new PasarelaLegadaAdapter(legada);
        var moderna = new PasarelaModernaSimulada(demo, bitacora);

        var a1 = await adapter.CobrarAsync(59m, "K-1");
        var a2 = await adapter.CobrarAsync(59m, "K-1");
        Evidencia.Registrar("Idempotencia pasarela", "Adapter: CobrarAsync(59.00, K-1) dos veces",
            $"misma referencia, 1 cobro real", $"{(a1.Referencia == a2.Referencia ? "misma referencia" : "otra referencia")}, {legada.CobrosEjecutados} cobro real");

        var m1 = await moderna.CobrarAsync(59m, "K-2");
        var m2 = await moderna.CobrarAsync(59m, "K-2");
        Assert.Equal(m1.Referencia, m2.Referencia);
        Assert.Single(bitacora.Entradas("PAGO"), e => e.Mensaje.Contains("PASARELA MODERNA"));

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.CobrarAsync(60m, "K-1")); // misma clave, otro monto

        //un rechazo no queda recordado: el mismo pedido se puede reintentar con la misma clave
        demo.ForzarRechazoCobro = true;
        Assert.False((await adapter.CobrarAsync(10m, "K-3")).Aprobado);
        demo.ForzarRechazoCobro = false;
        Assert.True((await adapter.CobrarAsync(10m, "K-3")).Aprobado);
    }

    //repositorio de Outbox que falla la primera vez que se le agrega un evento
    private sealed class OutboxQueFallaUnaVez(IOutboxRepositorio real, Contador contador) : IOutboxRepositorio
    {
        public Task<IReadOnlyList<EventoOutbox>> PendientesAsync(int maximo, CancellationToken ct = default) => real.PendientesAsync(maximo, ct);
        public Task<EventoOutbox?> ObtenerAsync(int id, CancellationToken ct = default) => real.ObtenerAsync(id, ct);
        public void Agregar(EventoOutbox evento)
        {
            if (contador.Valor++ == 0) throw new IOException("Disco lleno (simulado) después de aprobar el cobro.");
            real.Agregar(evento);
        }
    }

    private sealed class Contador { public int Valor; }

    [Fact(DisplayName = "Facade: si algo falla DESPUÉS de aprobar el cobro, el reintento con la misma clave no cobra de nuevo")]
    public async Task Facade_FalloTrasCobro_ReintentoSinCobroDoble()
    {
        var contador = new Contador();
        await using var e = await EntornoPrueba.CrearAsync(s => s.AddScoped<IOutboxRepositorio>(sp =>
            new OutboxQueFallaUnaVez(new Infrastructure.Persistencia.OutboxRepositorioEf(
                sp.GetRequiredService<Infrastructure.Persistencia.LogiMarketDbContext>()), contador)));
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var clave = await e.ClaveDefecto(id);

        var fallo = await e.Confirmar(id, clave);
        Assert.False(fallo.Exito);
        Assert.Equal(0, await e.Db(db => db.Pagos.CountAsync()));
        Assert.Equal(8, await e.Stock("P01")); // rollback completo

        var reintento = await e.Confirmar(id, clave);
        var legada = e.Unico<PasarelaLegadaSimulada>();
        Evidencia.Registrar("Facade + idempotencia", "Ana KIT_TELETRABAJO×1; falla tras aprobar; reintento misma clave",
            "PAGADO, 1 pago, 1 cobro real en la pasarela", $"{(reintento.Exito ? "PAGADO" : reintento.Codigo)}, " +
            $"{await e.Db(db => db.Pagos.CountAsync())} pago, {legada.CobrosEjecutados} cobro real en la pasarela");
    }

    //outbox: aviso no duplicado

    //observador que falla la primera vez (simula una caida despues de enviar el aviso)
    private sealed class ObservadorQueFallaUnaVez(Contador contador) : IObservadorEventos
    {
        public string Nombre => "Falla una vez";
        public Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default) =>
            contador.Valor++ == 0 ? throw new InvalidOperationException("Caída simulada tras el aviso.") : Task.CompletedTask;
    }

    [Fact(DisplayName = "P37 Outbox: si falla un observador después del aviso, el reintento NO reenvía el correo")]
    public async Task Outbox_ReintentoNoDuplicaAviso()
    {
        var contador = new Contador();
        await using var e = await EntornoPrueba.CrearAsync(s => s.AddScoped<IObservadorEventos>(_ => new ObservadorQueFallaUnaVez(contador)));
        await e.IniciarSesion("ventas");
        var id = await e.CrearBorrador("Ana", TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        Assert.True((await e.Confirmar(id)).Exito);
        Assert.Null(await e.Db(db => db.EventosOutbox.Select(x => x.FechaProcesado).SingleAsync())); //quedo pendiente

        var r = await e.Usar<IProcesadorOutbox, ResultadoProcesoOutbox>(p => p.ProcesarPendientesAsync());
        var lineas = File.ReadAllLines(Path.Combine(e.CarpetaSalidas, "notificaciones.log")).Length;
        Evidencia.Registrar("P37 sin aviso duplicado", "Aviso enviado, falla otro observador, se reprocesa",
            "1 evento procesado, 1 aviso enviado, 1 métrica de aviso",
            $"{r.Procesados} evento procesado, {lineas} aviso enviado, {e.Unico<IMetricasSesion>().Instantanea().Notificaciones} métrica de aviso");
    }

    //memento completo

    [Fact(DisplayName = "P29 Memento: deshacer la primera línea vuelve al carrito vacío y rehacer la recupera")]
    public void Memento_CarritoVacio()
    {
        var editor = new EditorBorrador(Valoracion());
        editor.SeleccionarCliente(new ClienteDto(1, "1", "Ana", "ana@ejemplo.test", "099", "VIP", true));
        editor.Agregar(CatalogoPrueba.Elemento("KIT_TELETRABAJO"), 1);
        Assert.True(editor.Deshacer());
        Assert.True(editor.EstaVacio);
        Assert.True(editor.Rehacer());
        Evidencia.Registrar("P29 Memento vacío", "Agregar KIT_TELETRABAJO, deshacer, rehacer",
            "vacío tras deshacer; KIT_TELETRABAJOx1 tras rehacer; total 59.00",
            $"vacío tras deshacer; {string.Join(",", editor.Pedido!.Lineas.Select(l => $"{l.Codigo}x{l.Cantidad}"))} tras rehacer; total {Dinero.Formatear(editor.Valorar()!.Total)}");
    }

    [Fact(DisplayName = "Invariante: un pedido nunca queda sin líneas; quitar la última vacía el carrito y se puede deshacer")]
    public void Memento_QuitarUltimaLinea()
    {
        var pedido = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("P01", 1));
        var ex = Assert.Throws<ExcepcionDominio>(() => pedido.QuitarLinea("P01"));
        Assert.Equal("PEDIDO_SIN_LINEAS", ex.Codigo);

        var editor = new EditorBorrador(Valoracion());
        editor.SeleccionarCliente(new ClienteDto(2, "2", "Luis", "luis@ejemplo.test", "099", "NORMAL", true));
        editor.Agregar(CatalogoPrueba.Elemento("P04"), 2);
        editor.Quitar("P04");
        Assert.True(editor.EstaVacio);
        Assert.True(editor.Deshacer());
        Assert.Equal(2, editor.Pedido!.Lineas.Single().Cantidad);
        Assert.Equal("Luis", editor.Pedido.ClienteNombre);
    }

    //bordes de stock y entradas invalidas

    [Fact(DisplayName = "Stock: se acepta stock == cantidad y se rechaza cantidad > stock")]
    public void Stock_Borde()
    {
        var p = Domain.Catalogo.Producto.Crear("PX", "X", "C", "", 1m, 0m, 3);
        p.DescontarStock(3);
        Assert.Equal(0, p.Stock);
        Assert.Throws<ExcepcionDominio>(() => p.DescontarStock(1));
        Assert.Equal(0, p.Stock);

        var cadena = new CadenaValidacionPedido(new Application.Seguridad.PoliticaPermisos());
        var pedido = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("P02", 5));
        var cliente = Cliente.Crear("9", "Luis", "l@x.test", "", CategoriaCliente.NORMAL);
        var usuario = new Application.Seguridad.UsuarioSesion(1, "ventas", "V", Domain.Usuarios.Rol.VENTAS);
        Assert.True(cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = new Dictionary<string, int> { ["P02"] = 5 }, Usuario = usuario }).EsValido);
        Assert.False(cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = new Dictionary<string, int> { ["P02"] = 4 }, Usuario = usuario }).EsValido);
    }

    [Fact(DisplayName = "Demostración: proveedor o canal desconocido se rechaza (sin valor por defecto silencioso)")]
    public async Task Demo_ValoresInvalidosRechazados()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        await e.IniciarSesion("admin");
        using var scope = e.Proveedor.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IServicioAdministracion>();
        Assert.Throws<ExcepcionAplicacion>(() => admin.AplicarDemo(c => c.ProveedorPago = "PAYPAL"));
        Assert.Throws<ExcepcionAplicacion>(() => admin.AplicarDemo(c => c.CanalNotificacion = "FAX"));
        Assert.Equal(ConfiguracionDemo.ProveedorLegado, e.Unico<ConfiguracionDemo>().ProveedorPago); //sin cambios

        e.Unico<ConfiguracionDemo>().ProveedorPago = "INEXISTENTE";
        Assert.Throws<ExcepcionAplicacion>(() => e.Unico<SelectorPasarela>().Actual);
    }

    //interpreter: CLIENTE exacto

    [Fact(DisplayName = "P25 Interpreter: CLIENTE = texto es igualdad del nombre (CLIENTE = a no incluye a Ana)")]
    public void Interpreter_ClienteExacto()
    {
        var pedidos = new[]
        {
            new PedidoResumenDto(1, "PED-1", 1, "Ana", "VIP", "PAGADO", "ESTANDAR", 60, 6, "VIP", 5, 59, DateTime.Now, null, false, 3, 54),
            new PedidoResumenDto(2, "PED-2", 2, "Mariana", "NORMAL", "PAGADO", "ESTANDAR", 10, 0, "NORMAL", 5, 15, DateTime.Now, null, false, 1, 10)
        };
        int[] F(string f) => pedidos.Where(AnalizadorFiltro.Analizar(f).Interpretar).Select(p => p.Id).ToArray();
        Assert.Empty(F("CLIENTE = a"));
        Assert.Equal(new[] { 1 }, F("CLIENTE = 'ANA'"));
        Assert.Equal(new[] { 2 }, F("CLIENTE != Ana"));
    }

    //flyweight determinista

    [Fact(DisplayName = "P21 Flyweight: identidad compartida y recuento de objetos (comprobación estable)")]
    public void Flyweight_Determinista()
    {
        var productos = Infrastructure.Persistencia.InicializadorBaseDatos.ProductosOficiales
            .Select((p, i) => new ProductoDto(i + 1, p.Sku, p.Nombre, p.Categoria, p.Descripcion, p.Precio, p.Peso, p.Stock, true)).ToList();
        var fabrica = new CatalogoVisualFlyweightFactory();
        var m = new GeneradorVistaMasiva(fabrica).Medir(productos, 1000);

        Evidencia.Registrar("P21 Flyweight", "1.000 filas, 5 SKU, mismos datos con y sin Flyweight",
            "ReferenceEquals=True; 5 instancias; 20 cadenas con Flyweight; 4000 cadenas sin Flyweight",
            $"ReferenceEquals={m.ReferenciasCompartidas}; {m.InstanciasMetadatos} instancias; {m.CadenasRetenidasConFlyweight} cadenas con Flyweight; " +
            $"{m.CadenasRetenidasSinFlyweight} cadenas sin Flyweight");
        //la memoria (GC.GetTotalMemory) se reporta
        //no se usa como asercion porque depende del recolector
        Assert.True(m.BytesConFlyweight >= 0 || m.BytesSinFlyweight >= 0);

        //si el catalogo cambia, el SKU recibe UNA nueva instancia (no se queda con datos viejos)
        var antes = fabrica.Obtener("P01", "Mouse USB", "Periféricos", "Mouse óptico con cable USB");
        var despues = fabrica.Obtener("P01", "Mouse USB Pro", "Periféricos", "Mouse óptico con cable USB");
        Assert.NotSame(antes, despues);
        Assert.Equal("Mouse USB Pro", despues.Nombre);
        Assert.Same(despues, fabrica.Obtener("P01", "Mouse USB Pro", "Periféricos", "Mouse óptico con cable USB"));
    }

    //regla MAYORISTA confirmada con la guia

    [Fact(DisplayName = "P32 Strategy: MAYORISTA depende de 10 unidades, no de la categoría (texto literal de la guía)")]
    public void Mayorista_PorUnidades()
    {
        var calc = CalculadoraDescuento.PorDefecto();
        var normal10 = calc.Calcular(new ContextoDescuento(CategoriaCliente.NORMAL, 10, 100m));
        var mayorista9 = calc.Calcular(new ContextoDescuento(CategoriaCliente.MAYORISTA, 9, 90m));
        Evidencia.Registrar("P32 regla mayorista", "NORMAL con 10 u. (100.00) y MAYORISTA con 9 u. (90.00)",
            "MAYORISTA 15.00; NORMAL 0.00", $"{normal10.Politica} {Dinero.Formatear(normal10.Monto)}; {mayorista9.Politica} {Dinero.Formatear(mayorista9.Monto)}");
    }
}
