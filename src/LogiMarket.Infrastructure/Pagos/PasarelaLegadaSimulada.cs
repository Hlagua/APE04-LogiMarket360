namespace LogiMarket.Infrastructure.Pagos;

//respuesta del sistema legado: codigo "00" aprobado, "51" fondos insuficientes, "96" error de sistema
public sealed class RespuestaLegada
{
    public string Codigo { get; init; } = "";
    public string NumeroAutorizacion { get; init; } = "";
    public string Texto { get; init; } = "";
}

//"Codigo LEGADO" (no se modifica):
//API sincrona e incompatible que cobra en CENTAVOS con una referencia de texto
//la aplicacion nunca la conoce
//solo el Adapter la usa
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
        if (_rechazar() || centavos > (long)(Domain.Comun.ParametrosNegocio.MontoMaximoCobroSimulado * 100)) return new RespuestaLegada { Codigo = "51", Texto = "FONDOS INSUFICIENTES" };
        var n = Interlocked.Increment(ref _secuencia);
        return new RespuestaLegada { Codigo = "00", NumeroAutorizacion = $"LEG{DateTime.Now:HHmmssfff}{n:0000}", Texto = "APROBADA" };
    }
}
