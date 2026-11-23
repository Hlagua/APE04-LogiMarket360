namespace LogiMarket.Domain.Comun;

/// <summary>
/// Valores fijados por la guía APE 04, con nombre y en un solo lugar (sin números mágicos repartidos).
/// Cambiar una regla del caso = cambiar una constante aquí.
/// </summary>
public static class ParametrosNegocio
{
    // ---- Promociones (excluyentes, gana la de mayor prioridad) ----
    public const decimal TasaVip = 0.10m;
    public const decimal TasaMayorista = 0.15m;

    /// <summary>MAYORISTA: unidades físicas finales mínimas, sin importar la categoría del cliente.</summary>
    public const int UnidadesMinimasMayorista = 10;

    // ---- Familias de entrega ----
    public const decimal TarifaEstandar = 5.00m;
    public const int PlazoEstandarDias = 3;
    public const decimal TarifaExpres = 12.00m;
    public const int PlazoExpresDias = 1;

    // ---- Devoluciones ----
    public const int DiasMaximosDefecto = 30;
    public const int DiasMaximosArrepentimiento = 7;

    // ---- Operación (decisiones de diseño, ver docs/BITACORA_DECISIONES.md) ----
    /// <summary>Cantidad máxima por línea aceptada por la validación del pedido.</summary>
    public const int CantidadMaximaPorLinea = 1000;

    /// <summary>Stock igual o menor a este valor se muestra como alerta.</summary>
    public const int UmbralStockBajo = 3;

    /// <summary>Monto máximo que autorizan las pasarelas simuladas (por encima, rechazo por fondos).</summary>
    public const decimal MontoMaximoCobroSimulado = 10_000m;

    /// <summary>Estados que guarda el historial de deshacer del borrador.</summary>
    public const int CapacidadHistorialBorrador = 5;
}
