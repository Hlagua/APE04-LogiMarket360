namespace LogiMarket.Infrastructure.Pagos;

/// <summary>Respuesta del sistema legado: código "00" aprobado, "51" fondos insuficientes, "96" error de sistema.</summary>
public sealed class RespuestaLegada
{
    public string Codigo { get; init; } = "";
    public string NumeroAutorizacion { get; init; } = "";
    public string Texto { get; init; } = "";
}

/// <summary>
/// "Código LEGADO" (no se modifica): API síncrona e incompatible que cobra en CENTAVOS con una
/// referencia de texto. La aplicación nunca la conoce; solo el Adapter la usa.
/// </summary>
public class PasarelaLegadaSimulada
{
    private readonly Func<bool> _rechazar;
    private readonly Func<bool> _fallar;
    private readonly Action<string> _traza;
    private int _secuencia;

    public PasarelaLegadaSimulada(Func<bool> rechazar, Func<bool> fallar, Action<string> traza)
    {
        _rechazar = rechazar;
        _fallar = fallar;
        _traza = traza;
    }

    public int CobrosEjecutados => _secuencia;

    public RespuestaLegada CobrarCentavos(long centavos, string referencia)
    {
        _traza($"PASARELA LEGADA CobrarCentavos({centavos}, \"{referencia}\")");
        if (_fallar()) throw new TimeoutException("La pasarela legada no respondió (simulado).");
        if (centavos <= 0) return new RespuestaLegada { Codigo = "13", Texto = "MONTO INVALIDO" };
        if (_rechazar() || centavos > 1_000_000) return new RespuestaLegada { Codigo = "51", Texto = "FONDOS INSUFICIENTES" };
        var n = Interlocked.Increment(ref _secuencia);
        return new RespuestaLegada { Codigo = "00", NumeroAutorizacion = $"LEG{DateTime.Now:HHmmssfff}{n:0000}", Texto = "APROBADA" };
    }
}
