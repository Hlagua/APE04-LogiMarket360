namespace LogiMarket.Domain.Pedidos;

public enum EstadoPedido
{
    BORRADOR,
    VALIDADO,
    PAGADO,
    DESPACHADO,
    ENTREGADO,
    CANCELADO
}

public enum TipoEntrega
{
    ESTANDAR,
    EXPRES
}
