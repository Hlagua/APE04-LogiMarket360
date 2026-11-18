using LogiMarket.Domain.Catalogo;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure.Persistencia;

namespace LogiMarket.Tests.Soporte;

/// <summary>Catálogo oficial en memoria (sin base) para pruebas unitarias del dominio.</summary>
public static class CatalogoPrueba
{
    public static ArbolCatalogo Arbol()
    {
        var productos = InicializadorBaseDatos.ProductosOficiales
            .Select(p => Producto.Crear(p.Sku, p.Nombre, p.Categoria, p.Descripcion, p.Precio, p.Peso, p.Stock)).ToList();
        var oficina = Kit.Crear("KIT_OFICINA", "Kit Oficina");
        oficina.AgregarComponente(productos.Single(p => p.Sku == "P01"), 1);
        oficina.AgregarComponente(productos.Single(p => p.Sku == "P02"), 1);
        var teletrabajo = Kit.Crear("KIT_TELETRABAJO", "Kit Teletrabajo");
        teletrabajo.AgregarComponente(oficina, 1);
        teletrabajo.AgregarComponente(productos.Single(p => p.Sku == "P03"), 1);
        return new ArbolCatalogo(productos, new[] { oficina, teletrabajo });
    }

    public static IElementoCatalogo Elemento(string codigo) => Arbol().Obtener(codigo);

    public static Pedido Pedido(CategoriaCliente categoria, TipoEntrega entrega, params (string Codigo, int Cantidad)[] lineas)
    {
        var arbol = Arbol();
        var b = new PedidoBuilder().ParaCliente(1, "Cliente Prueba", categoria).ConEntrega(entrega);
        foreach (var (c, n) in lineas) b.Agregar(arbol.Obtener(c), n);
        return b.Construir();
    }
}
