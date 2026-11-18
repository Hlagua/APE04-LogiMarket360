using LogiMarket.Application.Entrega;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Promociones;

namespace LogiMarket.Application.Pedidos;

public sealed record ResumenValoracion(decimal Subtotal, decimal Descuento, string Politica, decimal Tasa,
    decimal Tarifa, decimal Total, int PlazoDias, string Familia, int Unidades);

/// <summary>
/// Valoración única del pedido (DRY): Strategy de descuento + tarifa de la familia de entrega
/// (Abstract Factory). Ningún formulario calcula tarifas ni descuentos.
/// </summary>
public sealed class ServicioValoracion
{
    private readonly CalculadoraDescuento _calculadora;
    private readonly ProveedorFabricasEntrega _fabricas;

    public ServicioValoracion(CalculadoraDescuento calculadora, ProveedorFabricasEntrega fabricas)
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
