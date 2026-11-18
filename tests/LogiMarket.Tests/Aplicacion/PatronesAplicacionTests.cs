using System.Reflection;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Entrega;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Notificaciones;
using LogiMarket.Application.Pagos;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Reportes;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Eventos;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;
using LogiMarket.Domain.Usuarios;
using LogiMarket.Infrastructure.Documentos;
using LogiMarket.Infrastructure.Notificaciones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Registro;
using LogiMarket.Tests.Soporte;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.Tests.Aplicacion;

/// <summary>Salida de prueba del Bridge (sustituye a la VistaPrevia de WinForms).</summary>
public sealed class SalidaMemoria : ISalidaDocumento
{
    public List<(string Titulo, string Contenido)> Emitidos { get; } = new();
    public string Nombre => "VistaPrevia";
    public Task<ResultadoSalida> EmitirAsync(string titulo, string contenido, CancellationToken ct = default)
    {
        Emitidos.Add((titulo, contenido));
        return Task.FromResult(new ResultadoSalida(true, Nombre, "pantalla", contenido.Length));
    }
}

/// <summary>Pasarela espía para verificar si el Proxy llegó a invocarla.</summary>
public sealed class PasarelaEspia : IPasarelaPago
{
    public int Invocaciones { get; private set; }
    public bool Lanzar { get; set; }
    public string Nombre => "ESPIA";
    public Task<ResultadoCobro> CobrarAsync(decimal monto, string clave, CancellationToken ct = default)
    {
        Invocaciones++;
        if (Lanzar) throw new TimeoutException("fallo simulado");
        return Task.FromResult(new ResultadoCobro(true, "REF-" + clave, Nombre, "ok"));
    }
}

public class PatronesAplicacionTests
{
    private static SesionActual Sesion(Rol rol)
    {
        var s = new SesionActual();
        s.Establecer(new UsuarioSesion(1, rol.ToString().ToLower(), "Prueba", rol));
        return s;
    }

    private static Pedido PedidoAna() => CatalogoPrueba.Pedido(CategoriaCliente.VIP, TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));

    [Fact(DisplayName = "P12 Abstract Factory: ESTÁNDAR 5.00/3 días y EXPRÉS 12.00/1 día con guías de su familia")]
    public void P12_AbstractFactory()
    {
        var proveedor = new ProveedorFabricasEntrega(new IFabricaEntrega[] { new EntregaEstandarFactory(), new EntregaExpresFactory() });
        var hoy = new DateTime(2026, 11, 1);
        foreach (var (tipo, tarifa, dias, prefijo) in new[] { (TipoEntrega.ESTANDAR, 5.00m, 3, "STD-"), (TipoEntrega.EXPRES, 12.00m, 1, "EXP-") })
        {
            var f = proveedor.Para(tipo);
            var plazo = f.CrearCalculadoraPlazo();
            Assert.Equal(tarifa, f.CrearCalculadoraTarifa().Calcular(1m));
            Assert.Equal(dias, plazo.Dias);
            var guia = f.CrearGeneradorGuia().Generar(7, "Ana", 1m, hoy, plazo);
            Assert.StartsWith(prefijo, guia.Numero);
            Assert.Equal(hoy.AddDays(dias), guia.FechaEstimada);
        }
        // Mezclar familias se rechaza.
        var mezcla = Assert.Throws<ExcepcionDominio>(() => new EntregaExpresFactory().CrearGeneradorGuia()
            .Generar(1, "Ana", 1m, hoy, new EntregaEstandarFactory().CrearCalculadoraPlazo()));
        Assert.Equal("FAMILIA_MEZCLADA", mezcla.Codigo);
    }

    [Fact(DisplayName = "Valoración oficial: Ana 59.00, Luis 102.00, Carmen 90.00")]
    public void Valoracion_CasosOficiales()
    {
        var val = new ServicioValoracion(CalculadoraDescuento.PorDefecto(),
            new ProveedorFabricasEntrega(new IFabricaEntrega[] { new EntregaEstandarFactory(), new EntregaExpresFactory() }));
        var ana = val.Valorar(PedidoAna());
        Assert.Equal((60.00m, 6.00m, 5.00m, 59.00m), (ana.Subtotal, ana.Descuento, ana.Tarifa, ana.Total));
        var luis = val.Valorar(CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.EXPRES, ("P04", 2), ("P05", 2)));
        Assert.Equal((90.00m, 0.00m, 12.00m, 102.00m), (luis.Subtotal, luis.Descuento, luis.Tarifa, luis.Total));
        var carmen = val.Valorar(CatalogoPrueba.Pedido(CategoriaCliente.MAYORISTA, TipoEntrega.ESTANDAR, ("P03", 10)));
        Assert.Equal((100.00m, 15.00m, 90.00m), (carmen.Subtotal, carmen.Descuento, carmen.Total));
    }

    [Fact(DisplayName = "P23 Chain: primer error detiene la cadena; al corregirlo aparece el siguiente")]
    public void P23_Chain()
    {
        var cadena = new CadenaValidacionPedido(new PoliticaPermisos());
        var pedido = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 6));
        var stock = new Dictionary<string, int> { ["P01"] = 8, ["P02"] = 5, ["P03"] = 20 };
        var usuario = new UsuarioSesion(1, "ventas", "V", Rol.VENTAS);

        var r1 = cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = null, StockDisponible = stock, Usuario = usuario });
        Assert.Equal(("ClienteActivo", "CLIENTE_REQUERIDO"), (r1.Rechazo!.Eslabon, r1.Rechazo.Codigo));
        Assert.Single(r1.Recorrido);

        var cliente = Cliente.Crear("1", "Luis", "l@x.test", "", CategoriaCliente.NORMAL);
        var r2 = cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = stock, Usuario = usuario });
        Assert.Equal(("StockDisponible", "STOCK_INSUFICIENTE"), (r2.Rechazo!.Eslabon, r2.Rechazo.Codigo));
        Assert.Contains("P02", r2.Rechazo.Mensaje);
        Assert.Equal(new[] { "ClienteActivo", "LineasCantidades", "StockDisponible" }, r2.Recorrido);

        pedido.CambiarCantidad("KIT_TELETRABAJO", 1);
        var r3 = cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = stock, Usuario = new UsuarioSesion(2, "c", "C", Rol.CONSULTA) });
        Assert.Equal("NO_AUTORIZADO", r3.Rechazo!.Codigo);

        var r4 = cadena.Validar(new ContextoValidacion { Pedido = pedido, Cliente = cliente, StockDisponible = stock, Usuario = usuario });
        Assert.True(r4.EsValido);
        Assert.Equal(4, r4.Recorrido.Count);
    }

    private static readonly PedidoResumenDto[] PedidosFiltro =
    {
        Resumen(1, "Ana", "PAGADO", 59m), Resumen(2, "Luis", "PAGADO", 102m), Resumen(3, "Carmen", "PAGADO", 45m),
        Resumen(4, "Ana", "BORRADOR", 200m), Resumen(5, "Luis", "ENTREGADO", 30m)
    };

    private static PedidoResumenDto Resumen(int id, string cliente, string estado, decimal total) =>
        new(id, $"PED-{id}", id, cliente, "NORMAL", estado, "ESTANDAR", total, 0, "NORMAL", 0, total, DateTime.Now, null, false, 1);

    private static int[] Filtrar(string filtro) =>
        PedidosFiltro.Where(AnalizadorFiltro.Analizar(filtro).Interpretar).Select(p => p.Id).ToArray();

    [Fact(DisplayName = "P25 Interpreter: ESTADO = PAGADO AND TOTAL >= 50")]
    public void P25_Interpreter()
    {
        Assert.Equal(new[] { 1, 2 }, Filtrar("ESTADO = PAGADO AND TOTAL >= 50"));
        Assert.Equal(new[] { 1, 4 }, Filtrar("cliente = 'ana'"));
    }

    [Fact(DisplayName = "P26 Interpreter: precedencia AND/OR, paréntesis y errores de sintaxis")]
    public void P26_Interpreter_PrecedenciaYErrores()
    {
        // Sin paréntesis AND se evalúa antes que OR.
        Assert.Equal(new[] { 1, 2, 5 }, Filtrar("ESTADO = ENTREGADO OR ESTADO = PAGADO AND TOTAL >= 50"));
        Assert.Equal(new[] { 1, 2 }, Filtrar("(ESTADO = ENTREGADO OR ESTADO = PAGADO) AND TOTAL >= 50"));
        Assert.Equal("((ESTADO = ENTREGADO OR ESTADO = PAGADO) AND TOTAL >= 50.00)",
            AnalizadorFiltro.Analizar("(ESTADO = ENTREGADO OR ESTADO = PAGADO) AND TOTAL >= 50").Describir());

        Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("ESTADO = PAGADO AND"));
        Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("(ESTADO = PAGADO"));
        Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("TOTAL >= abc"));
        Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("ESTADO >= PAGADO"));
        Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("ESTADO = INEXISTENTE"));
        var inyeccion = Assert.Throws<ExcepcionSintaxisFiltro>(() => AnalizadorFiltro.Analizar("CLIENTE = x; DROP TABLE Pedido"));
        Assert.Contains("inesperado", inyeccion.Message);
    }

    [Fact(DisplayName = "P16 Adapter: proveedor legado y alternativo cumplen el mismo contrato; legado intacto")]
    public async Task P16_Adapter()
    {
        var demo = new ConfiguracionDemo();
        var bitacora = new BitacoraArchivo(null);
        var legada = new PasarelaLegadaSimulada(() => demo.ForzarRechazoCobro, () => demo.ForzarErrorPasarela, m => bitacora.Registrar("PAGO", m));
        IPasarelaPago[] proveedores = { new PasarelaLegadaAdapter(legada), new PasarelaModernaSimulada(demo, bitacora) };

        foreach (var p in proveedores)
        {
            var ok = await p.CobrarAsync(59.00m, "K-" + p.Nombre);
            Assert.True(ok.Aprobado);
            Assert.False(string.IsNullOrWhiteSpace(ok.Referencia));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => p.CobrarAsync(0m, "K"));  // misma precondición (LSP)
        }
        demo.ForzarRechazoCobro = true;
        foreach (var p in proveedores) Assert.False((await p.CobrarAsync(10m, "R-" + p.Nombre)).Aprobado);

        Assert.Contains(bitacora.Entradas("PAGO"), e => e.Mensaje.Contains("CobrarCentavos(5900"));
        Assert.Equal(1, legada.CobrosEjecutados);
        // La capa Application no conoce la operación legada.
        Assert.DoesNotContain(typeof(IPasarelaPago).Assembly.GetTypes(),
            t => t.GetMethods().Any(m => m.Name == "CobrarCentavos"));
    }

    [Fact(DisplayName = "P19 Decorator: INICIO → PASARELA → FIN/ERROR sin alterar el resultado")]
    public async Task P19_Decorator()
    {
        var demo = new ConfiguracionDemo();
        var bitacora = new BitacoraArchivo(null);
        var interna = new PasarelaModernaSimulada(demo, bitacora);
        var decorada = new AuditoriaPagoDecorator(interna, bitacora);

        var r = await decorada.CobrarAsync(59m, "K1");
        Assert.True(r.Aprobado);
        var secuencia = bitacora.Entradas("PAGO").Select(e => e.Mensaje.Split(' ')[0]).ToArray();
        Assert.Equal(new[] { "INICIO", "PASARELA", "FIN" }, secuencia);
        Assert.Single(bitacora.Entradas("PAGO").Select(e => e.Correlacion).Distinct()); // misma correlación
        Assert.Equal(r.CorrelacionId, bitacora.Entradas("PAGO")[0].Correlacion);

        demo.ForzarErrorPasarela = true;
        await Assert.ThrowsAsync<TimeoutException>(() => decorada.CobrarAsync(10m, "K2"));
        Assert.StartsWith("ERROR", bitacora.Entradas("PAGO").Last().Mensaje);
    }

    [Theory(DisplayName = "P22 Proxy: CONSULTA/BODEGA no alcanzan Decorator ni pasarela; VENTAS/ADMIN sí")]
    [InlineData(Rol.CONSULTA, false)]
    [InlineData(Rol.BODEGA, false)]
    [InlineData(Rol.VENTAS, true)]
    [InlineData(Rol.ADMIN, true)]
    public async Task P22_Proxy(Rol rol, bool autorizado)
    {
        var bitacora = new BitacoraArchivo(null);
        var espia = new PasarelaEspia();
        var proxy = new PagoAutorizadoProxy(new AuditoriaPagoDecorator(espia, bitacora), Sesion(rol), new PoliticaPermisos(), bitacora, new MetricasSesion());

        if (autorizado)
        {
            Assert.True((await proxy.CobrarAsync(10m, "K")).Aprobado);
            Assert.Equal(1, espia.Invocaciones);
            Assert.Equal(2, bitacora.Entradas("PAGO").Count);
        }
        else
        {
            await Assert.ThrowsAsync<AccesoDenegadoException>(() => proxy.CobrarAsync(10m, "K"));
            Assert.Equal(0, espia.Invocaciones);
            Assert.Empty(bitacora.Entradas("PAGO"));          // sin LOG de cobro
            Assert.Single(bitacora.Entradas("SEGURIDAD"));    // auditoría de seguridad separada
        }
    }

    [Fact(DisplayName = "P11 Factory Method: EMAIL/SMS producen su notificador y registran el aviso correcto")]
    public async Task P11_FactoryMethod()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "logimarket-fm-" + Guid.NewGuid().ToString("N"));
        var demo = new ConfiguracionDemo();
        var bitacora = new BitacoraArchivo(null);
        var selector = new SelectorCanalNotificacion(new CreadorNotificador[]
        {
            new CreadorNotificadorEmail(carpeta, demo, bitacora), new CreadorNotificadorSms(carpeta, demo, bitacora)
        }, demo);
        var evento = new PedidoPagado(1, "Ana", "ana@ejemplo.test", "0990000001", DateTime.Now, 59m, "REF");

        var email = await selector.Actual.NotificarAsync(evento);
        demo.CanalNotificacion = ConfiguracionDemo.CanalSms;
        var sms = await selector.Actual.NotificarAsync(evento);

        Assert.StartsWith(nameof(NotificadorEmailSimulado), email);
        Assert.Contains("ana@ejemplo.test", email);
        Assert.StartsWith(nameof(NotificadorSmsSimulado), sms);
        var lineas = await File.ReadAllLinesAsync(Path.Combine(carpeta, "notificaciones.log"));
        Assert.Contains("[EMAIL]", lineas[0]);
        Assert.Contains("[SMS] Tel: 0990000001", lineas[1]);
        Directory.Delete(carpeta, true);
    }

    [Fact(DisplayName = "P17 Bridge: dos comprobantes × dos medios = cuatro combinaciones reales")]
    public async Task P17_Bridge()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "logimarket-br-" + Guid.NewGuid().ToString("N"));
        var resumen = Resumen(1, "Ana", "ENTREGADO", 59m) with { Subtotal = 60m, Descuento = 6m, Tarifa = 5m, Politica = "VIP" };
        var pedido = new PedidoCompletoDto(resumen, "ana@ejemplo.test",
            new[] { new LineaPedidoDto("KIT_TELETRABAJO", "Kit Teletrabajo", true, 1, 60m, 60m, "P01x1 + P02x1 + P03x1") },
            Array.Empty<HistorialEstadoDto>(), new PagoDto(1, "REF1", "K", 59m, "LEGADO", "c", DateTime.Now), null,
            new DevolucionDto(1, "DEFECTO", DateTime.Now, 20, 54m, "RMB-1", true, ""));

        var vista = new SalidaMemoria();
        var txt = new SalidaArchivoTxt(carpeta);
        var resultados = new List<ResultadoSalida>();
        foreach (var salida in new ISalidaDocumento[] { vista, txt })
        {
            resultados.Add(await new ComprobanteVenta(pedido, salida).EmitirAsync());
            resultados.Add(await new ComprobanteDevolucion(pedido, salida).EmitirAsync());
        }

        Assert.Equal(4, resultados.Count(r => r.Exito));
        Assert.Equal(2, vista.Emitidos.Count);
        Assert.Contains("TOTAL USD:", vista.Emitidos[0].Contenido);
        Assert.Contains("REEMBOLSO USD: 54.00", vista.Emitidos[1].Contenido);
        var archivos = Directory.GetFiles(carpeta, "*.txt");
        Assert.Equal(2, archivos.Length);
        Assert.Contains(archivos, a => File.ReadAllText(a).Contains("59.00"));
        Directory.Delete(carpeta, true);
    }

    [Fact(DisplayName = "P21 Flyweight: 1.000 filas comparten metadatos (ReferenceEquals) y se mide memoria equivalente")]
    public void P21_Flyweight()
    {
        var productos = Infrastructure.Persistencia.InicializadorBaseDatos.ProductosOficiales
            .Select((p, i) => new ProductoDto(i + 1, p.Sku, p.Nombre, p.Categoria, p.Descripcion + new string('.', 200), p.Precio, p.Peso, p.Stock, true))
            .ToList();
        var fabrica = new CatalogoVisualFlyweightFactory();
        var generador = new GeneradorVistaMasiva(fabrica);

        var medicion = generador.Medir(productos, 1000);
        var filas = generador.GenerarConFlyweight(productos, 1000);

        Assert.Equal(1000, filas.Count);
        Assert.Equal(5, fabrica.InstanciasCreadas);
        Assert.True(ReferenceEquals(filas[0].Metadatos, filas[5].Metadatos));
        Assert.True(medicion.ReferenciasCompartidas);
        Assert.True(medicion.BytesConFlyweight < medicion.BytesSinFlyweight,
            $"con={medicion.BytesConFlyweight} sin={medicion.BytesSinFlyweight}");
        Assert.NotEqual(filas[0].Stock + filas[0].Cantidad, filas[5].Stock + filas[5].Cantidad + 1000); // extrínseco por fila
    }

    [Fact(DisplayName = "P15 Singleton: dos consumidores reciben la misma instancia; contadores thread-safe")]
    public async Task P15_Singleton()
    {
        await using var e = await EntornoPrueba.CrearAsync();
        var vistaA = e.Proveedor.GetRequiredService<MetricasSesion>();
        using var scope = e.Proveedor.CreateScope();
        var vistaB = scope.ServiceProvider.GetRequiredService<MetricasSesion>();
        Assert.Same(vistaA, vistaB);

        Parallel.For(0, 1000, _ => vistaA.RegistrarNotificacion());
        Parallel.For(0, 500, _ => vistaB.RegistrarDespacho());
        var foto = vistaB.Instantanea();
        Assert.Equal((1000, 500), (foto.Notificaciones, foto.Despachos));
        Assert.Equal(vistaA.InstanciaId, foto.InstanciaId);
    }

    [Fact(DisplayName = "P38 ISP/DIP: interfaces pequeñas y Application/Domain sin dependencias de infraestructura")]
    public void P38_IspDip()
    {
        // ISP: ningún puerto obliga a más de 6 miembros; cobro y reembolso están segregados.
        var puertos = typeof(IPasarelaPago).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace!.EndsWith("Puertos"));
        Assert.All(puertos, p => Assert.True(p.GetMethods().Length <= 6, $"{p.Name} tiene {p.GetMethods().Length} miembros"));
        Assert.DoesNotContain(typeof(IPasarelaPago).GetMethods(), m => m.Name.Contains("Reembols"));

        // DIP / regla de dependencia: verificada en los ensamblados compilados.
        static string[] Refs(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!).ToArray();
        var dominio = Refs(typeof(Pedido).Assembly);
        var aplicacion = Refs(typeof(IPasarelaPago).Assembly);
        Assert.DoesNotContain(dominio, n => n.StartsWith("LogiMarket") || n.Contains("EntityFramework") || n.Contains("Sqlite") || n.Contains("Forms"));
        Assert.DoesNotContain(aplicacion, n => n.Contains("Infrastructure") || n.Contains("EntityFramework") || n.Contains("Sqlite") || n.Contains("Forms"));
        Assert.Contains("LogiMarket.Domain", aplicacion);
    }
}
