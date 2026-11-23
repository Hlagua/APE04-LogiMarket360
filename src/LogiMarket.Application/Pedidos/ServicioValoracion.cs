using LogiMarket.Application.Entrega;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Pedidos;

public sealed record ResumenValoracion(decimal Subtotal, decimal Descuento, string Politica, decimal Tasa,
    decimal Tarifa, decimal Total, int PlazoDias, string Familia, int Unidades);

//valoracion unica del pedido (DRY):
//strategy de descuento + tarifa de la familia de entrega (Abstract Factory)
//ningun formulario calcula tarifas ni descuentos
public sealed class ServicioValoracion : IServicioValoracion
{
    private readonly ICalculadoraDescuento _calculadora;
    private readonly IProveedorFabricasEntrega _fabricas;

    public ServicioValoracion(ICalculadoraDescuento calculadora, IProveedorFabricasEntrega fabricas)
    {
        _calculadora = calculadora;
        _fabricas = fabricas;
    }

    public ResumenValoracion Valorar(Pedido pedido)
    {
        var paquete = _fabricas.Preparar(pedido.TipoEntrega, pedido.PesoKg);
        var descuento = pedido.AplicarValoracion(_calculadora, paquete.Tarifa);
        return new ResumenValoracion(pedido.Subtotal, pedido.Descuento, descuento.Politica, descuento.Tasa,
            pedido.TarifaEnvio, pedido.Total, paquete.PlazoDias, paquete.Familia, pedido.UnidadesFisicas);
    }
}
