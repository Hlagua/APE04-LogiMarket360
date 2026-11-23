using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Comun;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;
using LogiMarket.Tests.Soporte;

namespace LogiMarket.Tests.Dominio;

//pruebas unitarias del dominio (sin base de datos)
public class PatronesDominioTests
{
    [Fact(DisplayName = "P18 Composite: KIT_OFICINA = 50.00 y KIT_TELETRABAJO = 60.00 con expansión a SKU finales")]
    public void P18_Composite_PreciosYExpansion()
    {
        var arbol = CatalogoPrueba.Arbol();
        Assert.Equal(50.00m, arbol.Obtener("KIT_OFICINA").Precio);
        var tele = arbol.Obtener("KIT_TELETRABAJO");
        Assert.Equal(60.00m, tele.Precio);
        Assert.Equal(new Dictionary<string, int> { ["P01"] = 1, ["P02"] = 1, ["P03"] = 1 }, tele.ExpandirSkus());
    }

    [Fact(DisplayName = "P18 Composite: un kit no puede contenerse directa ni indirectamente")]
    public void P18_Composite_RechazaCiclos()
    {
        var arbol = CatalogoPrueba.Arbol();
        var oficina = (KitCompuesto)arbol.Obtener("KIT_OFICINA");
        var tele = arbol.Obtener("KIT_TELETRABAJO");
        var ex = Assert.Throws<ExcepcionDominio>(() => oficina.Agregar(tele, 1)); //indirecto
        Assert.Equal("KIT_CICLO", ex.Code());
        Assert.Throws<ExcepcionDominio>(() => oficina.Agregar(oficina, 1)); //directo

        //a nivel de entidades persistidas (deteccion en ArbolCatalogo)
        var a = Kit.Crear("KA", "A");
        var b = Kit.Crear("KB", "B");
        var p = Producto.Crear("PX", "X", "Cat", "", 1, 0, 1);
        a.AgregarComponente(b, 1);
        b.AgregarComponente(a, 1);
        b.AgregarComponente(p, 1);
        var cic = Assert.Throws<ExcepcionDominio>(() => new ArbolCatalogo(new[] { p }, new[] { a, b }).ValidarIntegridad());
        Assert.Equal("KIT_CICLO", cic.Codigo);
    }

    [Fact(DisplayName = "P27 Iterator: KIT_TELETRABAJO recorre P01, P02, P03 en orden y termina")]
    public void P27_Iterator()
    {
        var kit = (KitCompuesto)new KitCompuesto("K2", "Doble").Agregar(CatalogoPrueba.Elemento("KIT_TELETRABAJO"), 2);
        var it = CatalogoPrueba.Elemento("KIT_TELETRABAJO").CrearIterador();
        var recorrido = new List<string>();
        var pasos = 0;
        while (it.MoverSiguiente() && ++pasos < 100) recorrido.Add($"{it.Actual.Sku}x{it.Actual.Cantidad}");
        Assert.Equal(new[] { "P01x1", "P02x1", "P03x1" }, recorrido);
        Assert.False(it.MoverSiguiente()); //termino

        //las cantidades se propagan por nivel
        Assert.Equal(new[] { 2, 2, 2 }, kit.ComponentesFisicos().Select(c => c.Cantidad));
    }

    [Fact(DisplayName = "P34 Visitor: desglose P01/P02/P03 y peso logístico 1.00 kg con visita anidada")]
    public void P34_Visitor()
    {
        var tele = CatalogoPrueba.Elemento("KIT_TELETRABAJO");
        var desglose = new DesgloseSkuVisitor();
        var peso = new PesoLogisticoVisitor();
        tele.Aceptar(desglose);
        tele.Aceptar(peso);
        Assert.Equal(new[] { "P01", "P02", "P03" }, desglose.Lineas.Select(l => l.Sku));
        Assert.Equal(new[] { 2, 2, 1 }, desglose.Lineas.Select(l => l.Nivel)); //P01/P02 dentro de KIT_OFICINA
        Assert.Equal(1.00m, peso.PesoTotalKg);
        Assert.Equal(2, peso.KitsAtravesados);
    }

    [Fact(DisplayName = "P32 Strategy: VIP 6.00, MAYORISTA 15.00, NORMAL 0.00, sin acumulación")]
    public void P32_Strategy()
    {
        var calc = CalculadoraDescuento.PorDefecto();
        Assert.Equal(6.00m, calc.Calcular(new ContextoDescuento(CategoriaCliente.VIP, 3, 60m)).Monto);
        Assert.Equal(15.00m, calc.Calcular(new ContextoDescuento(CategoriaCliente.MAYORISTA, 10, 100m)).Monto);
        Assert.Equal(0.00m, calc.Calcular(new ContextoDescuento(CategoriaCliente.NORMAL, 4, 90m)).Monto);
        //VIP con 10 unidades: prioridad MAYORISTA 15 % (no 25 %)
        var vipMayorista = calc.Calcular(new ContextoDescuento(CategoriaCliente.VIP, 10, 100m));
        Assert.Equal(("MAYORISTA", 15.00m), (vipMayorista.Politica, vipMayorista.Monto));
    }

    private sealed class Aniversario20 : IPoliticaDescuento
    {
        public string Codigo => "ANIVERSARIO";
        public int Prioridad => 30;
        public decimal Tasa => 0.20m;
        public bool Aplica(ContextoDescuento c) => c.Subtotal >= 500m;
    }

    [Fact(DisplayName = "P32 Strategy: nueva promoción se agrega sin editar la calculadora (OCP)")]
    public void P32_Strategy_NuevaPolitica()
    {
        var calc = new CalculadoraDescuento(new IPoliticaDescuento[] { new SinDescuento(), new Vip10(), new Mayorista15(), new Aniversario20() });
        Assert.Equal(("ANIVERSARIO", 100.00m), (calc.Calcular(new ContextoDescuento(CategoriaCliente.VIP, 12, 500m)) is var r ? (r.Politica, r.Monto) : default));
        Assert.Equal("VIP", calc.Calcular(new ContextoDescuento(CategoriaCliente.VIP, 1, 100m)).Politica);
    }

    [Fact(DisplayName = "P31 State: ciclo válido y transiciones inválidas rechazadas")]
    public void P31_State()
    {
        var p = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("P01", 1));
        var f = DateTime.Now;
        p.Validar(f, "t");
        Assert.Throws<ExcepcionDominio>(() => p.Validar(f, "t"));          // estado repetido
        Assert.Throws<ExcepcionDominio>(() => p.Despachar(f, "t", "G"));   // despachar antes de pagar
        p.RegistrarPago("K1", f, "t");
        Assert.Throws<ExcepcionDominio>(() => p.Cancelar(f, "t", "x"));    // PAGADO → CANCELADO inválido
        Assert.Throws<ExcepcionDominio>(() => p.RegistrarPago("K2", f, "t")); // cobro doble
        Assert.Throws<ExcepcionDominio>(() => p.AgregarLinea(CatalogoPrueba.Elemento("P02"), 1)); // editar PAGADO
        p.Despachar(f, "t", "G");
        p.Entregar(f, "t");
        Assert.Equal(EstadoPedido.ENTREGADO, p.Estado);
        Assert.Equal(new[] { "VALIDADO", "PAGADO", "DESPACHADO", "ENTREGADO" }, p.Historial.Select(h => h.EstadoNuevo.ToString()));
    }

    [Fact(DisplayName = "P13 Builder: pedido sin cliente o sin líneas rechazado; completo es válido")]
    public void P13_Builder()
    {
        var sinCliente = Assert.Throws<ExcepcionDominio>(() => new PedidoBuilder().Agregar(CatalogoPrueba.Elemento("P01"), 1).Construir());
        Assert.Equal("PEDIDO_SIN_CLIENTE", sinCliente.Codigo);
        var sinLineas = Assert.Throws<ExcepcionDominio>(() => new PedidoBuilder().ParaCliente(1, "Ana", CategoriaCliente.VIP).Construir());
        Assert.Equal("PEDIDO_SIN_LINEAS", sinLineas.Codigo);

        var p = CatalogoPrueba.Pedido(CategoriaCliente.VIP, TipoEntrega.EXPRES, ("KIT_TELETRABAJO", 1), ("P04", 2), ("P04", 1));
        Assert.Equal(EstadoPedido.BORRADOR, p.Estado);
        Assert.Equal(2, p.Lineas.Count);
        Assert.Equal(3, p.Lineas.Single(l => l.Codigo == "P04").Cantidad);
        Assert.Equal(180.00m, p.SubtotalCalculado);
    }

    [Fact(DisplayName = "P14 Prototype: copia profunda del carrito con KIT_TELETRABAJO")]
    public void P14_Prototype()
    {
        var plantilla = CatalogoPrueba.Pedido(CategoriaCliente.VIP, TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        plantilla.Validar(DateTime.Now, "t");
        plantilla.RegistrarPago("K", DateTime.Now, "t");

        var copia1 = plantilla.Clonar();
        var copia2 = plantilla.Clonar();
        copia1.CambiarCantidad("KIT_TELETRABAJO", 4);

        Assert.Equal(EstadoPedido.BORRADOR, copia1.Estado);
        Assert.Null(copia1.ClaveIdempotencia);
        Assert.Equal(0, copia1.Id);
        Assert.Equal(1, plantilla.Lineas.Single().Cantidad);
        Assert.Equal(1, copia2.Lineas.Single().Cantidad);
        Assert.NotSame(plantilla.Lineas, copia1.Lineas);
        Assert.NotSame(plantilla.Lineas.Single(), copia1.Lineas.Single());
        Assert.NotSame(plantilla.Lineas.Single().Componentes.First(), copia1.Lineas.Single().Componentes.First());
        Assert.Equal(3, copia1.Lineas.Single().Componentes.Count);
    }

    [Fact(DisplayName = "P29 Memento: tres ediciones, dos deshacer y un rehacer; bloqueado al confirmar")]
    public void P29_Memento()
    {
        var p = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("KIT_TELETRABAJO", 1));
        var h = new HistorialBorrador();
        string Foto() => $"{p.TipoEntrega}|" + string.Join(",", p.Lineas.Select(l => $"{l.Codigo}x{l.Cantidad}"));

        var s0 = Foto();
        h.Capturar(p); p.AgregarLinea(CatalogoPrueba.Elemento("P04"), 2); var s1 = Foto();
        h.Capturar(p); p.CambiarCantidad("KIT_TELETRABAJO", 3); var s2 = Foto();
        h.Capturar(p); p.CambiarEntrega(TipoEntrega.EXPRES);

        Assert.True(h.Deshacer(p)); Assert.Equal(s2, Foto());
        Assert.True(h.Deshacer(p)); Assert.Equal(s1, Foto());
        Assert.True(h.Rehacer(p)); Assert.Equal(s2, Foto());
        Assert.Equal(3, p.Lineas.Single(l => l.Codigo == "KIT_TELETRABAJO").Componentes.Count); // ítems anidados restaurados
        Assert.NotEqual(s0, Foto());

        p.Validar(DateTime.Now, "t");
        p.RegistrarPago("K", DateTime.Now, "t");
        h.Bloquear();
        Assert.False(h.Deshacer(p));
        Assert.Throws<ExcepcionDominio>(() => p.Restaurar(p.CrearMemento())); //no se deshace un pago
    }

    [Fact(DisplayName = "Memento: capacidad máxima de cinco estados")]
    public void Memento_Capacidad()
    {
        var p = CatalogoPrueba.Pedido(CategoriaCliente.NORMAL, TipoEntrega.ESTANDAR, ("P01", 1));
        var h = new HistorialBorrador();
        for (var i = 2; i <= 8; i++) { h.Capturar(p); p.CambiarCantidad("P01", i); }
        Assert.Equal(HistorialBorrador.Capacidad, h.CantidadDeshacer);
    }

    [Fact(DisplayName = "Dominio: invariantes de producto (SKU, precio positivo, peso y stock no negativos)")]
    public void Invariantes_Producto()
    {
        Assert.Throws<ExcepcionDominio>(() => Producto.Crear("", "X", "C", "", 1, 0, 0));
        Assert.Throws<ExcepcionDominio>(() => Producto.Crear("X1", "X", "C", "", 0, 0, 0));
        Assert.Throws<ExcepcionDominio>(() => Producto.Crear("X1", "X", "C", "", 1, -1, 0));
        Assert.Throws<ExcepcionDominio>(() => Producto.Crear("X1", "X", "C", "", 1, 0, -1));
        var p = Producto.Crear("x1", "X", "C", "", 1.005m, 0, 2);
        Assert.Equal("X1", p.Sku);
        Assert.Equal(1.01m, p.Precio); //AwayFromZero
        Assert.Throws<ExcepcionDominio>(() => p.DescontarStock(3));
        Assert.Equal(2, p.Stock);
    }
}

internal static class ExcepcionExtensiones
{
    public static string Code(this ExcepcionDominio e) => e.Codigo;
}
