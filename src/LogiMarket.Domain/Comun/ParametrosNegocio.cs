namespace LogiMarket.Domain.Comun;

//valores fijados por la guia APE 04, con nombre y en un solo lugar (sin numeros magicos repartidos)
//cambiar una regla del caso = cambiar una constante aqui
public static class ParametrosNegocio
{
    //promociones (excluyentes, gana la de mayor prioridad)
    public const decimal TasaVip = 0.10m;
    public const decimal TasaMayorista = 0.15m;

    //MAYORISTA: unidades fisicas finales minimas, sin importar la categoria del cliente
    public const int UnidadesMinimasMayorista = 10;

    //familias de entrega
    public const decimal TarifaEstandar = 5.00m;
    public const int PlazoEstandarDias = 3;
    public const decimal TarifaExpres = 12.00m;
    public const int PlazoExpresDias = 1;

    //devoluciones
    public const int DiasMaximosDefecto = 30;
    public const int DiasMaximosArrepentimiento = 7;

    //operacion (decisiones de diseño, ver docs/BITACORA_DECISIONES.md)
    //cantidad maxima por linea aceptada por la validacion del pedido
    public const int CantidadMaximaPorLinea = 1000;

    //stock igual o menor a este valor se muestra como alerta
    public const int UmbralStockBajo = 3;

    //monto maximo que autorizan las pasarelas simuladas (por encima, rechazo por fondos)
    public const decimal MontoMaximoCobroSimulado = 10_000m;

    //estados que guarda el historial de deshacer del borrador
    public const int CapacidadHistorialBorrador = 5;
}
