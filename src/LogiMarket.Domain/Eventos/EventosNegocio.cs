namespace LogiMarket.Domain.Eventos;

//evento de negocio publicado a los observadores despues de confirmar la transaccion
public abstract record EventoNegocio(int PedidoId, string ClienteNombre, string ClienteEmail, string ClienteTelefono, DateTime Fecha)
{
    public abstract string Tipo { get; }
    public abstract string Mensaje { get; }
    public int EventoOutboxId { get; init; }
}

public sealed record PedidoPagado(int PedidoId, string ClienteNombre, string ClienteEmail, string ClienteTelefono,
    DateTime Fecha, decimal Total, string ReferenciaPago)
    : EventoNegocio(PedidoId, ClienteNombre, ClienteEmail, ClienteTelefono, Fecha)
{
    public const string NombreTipo = "PedidoPagado";
    public override string Tipo => NombreTipo;
    public override string Mensaje => $"Su pedido PED-{PedidoId:000000} fue pagado por USD {Comun.Dinero.Formatear(Total)} (ref. {ReferenciaPago}).";
}

public sealed record DespachoRegistrado(int PedidoId, string ClienteNombre, string ClienteEmail, string ClienteTelefono,
    DateTime Fecha, string NumeroGuia, string Familia)
    : EventoNegocio(PedidoId, ClienteNombre, ClienteEmail, ClienteTelefono, Fecha)
{
    public const string NombreTipo = "DespachoRegistrado";
    public override string Tipo => NombreTipo;
    public override string Mensaje => $"Su pedido PED-{PedidoId:000000} fue despachado ({Familia}), guía {NumeroGuia}.";
}

public sealed record DevolucionAceptada(int PedidoId, string ClienteNombre, string ClienteEmail, string ClienteTelefono,
    DateTime Fecha, decimal MontoReembolso, string Motivo)
    : EventoNegocio(PedidoId, ClienteNombre, ClienteEmail, ClienteTelefono, Fecha)
{
    public const string NombreTipo = "DevolucionAceptada";
    public override string Tipo => NombreTipo;
    public override string Mensaje => $"Devolución ({Motivo}) del pedido PED-{PedidoId:000000} aceptada; reembolso USD {Comun.Dinero.Formatear(MontoReembolso)}.";
}
