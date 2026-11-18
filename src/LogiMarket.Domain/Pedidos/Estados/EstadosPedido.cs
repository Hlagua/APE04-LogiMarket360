using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Pedidos.Estados;

/// <summary>
/// Patrón State: cada estado decide qué acciones son válidas y cuál es el siguiente estado.
/// El agregado Pedido delega en el objeto de estado en lugar de usar un switch gigante.
/// </summary>
public abstract class EstadoPedidoBase
{
    public abstract EstadoPedido Codigo { get; }

    /// <summary>¿Se pueden modificar líneas, cantidades, precios o forma de entrega?</summary>
    public virtual bool PermiteEdicion => false;

    /// <summary>¿Se puede (re)calcular subtotal, descuento y tarifa?</summary>
    public virtual bool PermiteValoracion => false;

    public virtual bool PermiteCobro => false;

    public virtual EstadoPedido Validar() => throw Invalida("validar");
    public virtual EstadoPedido Pagar() => throw Invalida("pagar");
    public virtual EstadoPedido Despachar() => throw Invalida("despachar");
    public virtual EstadoPedido Entregar() => throw Invalida("entregar");
    public virtual EstadoPedido Cancelar() => throw Invalida("cancelar");
    public virtual void ExigirDevolucionPosible() => throw Invalida("devolver");

    public void ExigirEdicion()
    {
        if (!PermiteEdicion) throw Invalida("modificar");
    }

    protected ExcepcionDominio Invalida(string accion) =>
        new("TRANSICION_INVALIDA", $"No se puede {accion} un pedido en estado {Codigo}.");

    public override string ToString() => Codigo.ToString();
}

public sealed class EstadoBorrador : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.BORRADOR;
    public override bool PermiteEdicion => true;
    public override bool PermiteValoracion => true;
    public override EstadoPedido Validar() => EstadoPedido.VALIDADO;
    public override EstadoPedido Cancelar() => EstadoPedido.CANCELADO;
}

public sealed class EstadoValidado : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.VALIDADO;
    public override bool PermiteValoracion => true;
    public override bool PermiteCobro => true;
    public override EstadoPedido Pagar() => EstadoPedido.PAGADO;
    public override EstadoPedido Cancelar() => EstadoPedido.CANCELADO;
}

public sealed class EstadoPagado : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.PAGADO;
    public override EstadoPedido Despachar() => EstadoPedido.DESPACHADO;
}

public sealed class EstadoDespachado : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.DESPACHADO;
    public override EstadoPedido Entregar() => EstadoPedido.ENTREGADO;
}

public sealed class EstadoEntregado : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.ENTREGADO;
    public override void ExigirDevolucionPosible() { }
}

public sealed class EstadoCancelado : EstadoPedidoBase
{
    public override EstadoPedido Codigo => EstadoPedido.CANCELADO;
}

/// <summary>Registro de estados (sin estado interno, se comparten).</summary>
public static class EstadosPedido
{
    private static readonly IReadOnlyDictionary<EstadoPedido, EstadoPedidoBase> Mapa =
        new EstadoPedidoBase[]
        {
            new EstadoBorrador(), new EstadoValidado(), new EstadoPagado(),
            new EstadoDespachado(), new EstadoEntregado(), new EstadoCancelado()
        }.ToDictionary(e => e.Codigo);

    public static EstadoPedidoBase De(EstadoPedido codigo) => Mapa[codigo];
}
