using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Infrastructure.Pagos;

/// <summary>
/// Patrón Adapter: adapta <see cref="PasarelaLegadaSimulada.CobrarCentavos(long, string)"/> al contrato
/// <see cref="IPasarelaPago.CobrarAsync"/> (decimal + clave) sin modificar el código legado.
/// </summary>
public sealed class PasarelaLegadaAdapter : IPasarelaPago
{
    private readonly PasarelaLegadaSimulada _legada;

    public PasarelaLegadaAdapter(PasarelaLegadaSimulada legada) => _legada = legada;

    public string Nombre => ConfiguracionDemo.ProveedorLegado;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        ValidarEntrada(monto, claveIdempotencia);
        var centavos = (long)(Dinero.Redondear(monto) * 100m);
        var respuesta = _legada.CobrarCentavos(centavos, claveIdempotencia);
        var aprobado = respuesta.Codigo == "00";
        return Task.FromResult(new ResultadoCobro(aprobado, aprobado ? respuesta.NumeroAutorizacion : $"RECH-{respuesta.Codigo}",
            Nombre, $"{respuesta.Codigo} {respuesta.Texto}"));
    }

    internal static void ValidarEntrada(decimal monto, string clave)
    {
        // Precondiciones idénticas en ambos proveedores (LSP).
        if (monto <= 0) throw new ArgumentOutOfRangeException(nameof(monto), "El monto debe ser positivo.");
        if (string.IsNullOrWhiteSpace(clave)) throw new ArgumentException("La clave de idempotencia es obligatoria.", nameof(clave));
    }
}

/// <summary>Segundo proveedor simulado, nativamente compatible y sustituible por DI.</summary>
public sealed class PasarelaModernaSimulada : IPasarelaPago
{
    private readonly ConfiguracionDemo _demo;
    private readonly IBitacoraTecnica _bitacora;
    private static int _secuencia;

    public PasarelaModernaSimulada(ConfiguracionDemo demo, IBitacoraTecnica bitacora)
    {
        _demo = demo;
        _bitacora = bitacora;
    }

    public string Nombre => ConfiguracionDemo.ProveedorModerno;

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        PasarelaLegadaAdapter.ValidarEntrada(monto, claveIdempotencia);
        _bitacora.Registrar("PAGO", $"PASARELA MODERNA charge(amount={Dinero.Formatear(monto)}, idempotencyKey={claveIdempotencia})");
        await Task.Yield();
        if (_demo.ForzarErrorPasarela) throw new TimeoutException("La pasarela moderna no respondió (simulado).");
        if (_demo.ForzarRechazoCobro || monto > 10_000m)
            return new ResultadoCobro(false, "RECH-DECLINED", Nombre, "declined (simulado)");
        var n = Interlocked.Increment(ref _secuencia);
        return new ResultadoCobro(true, $"MOD-{Guid.NewGuid():N}"[..16] + n, Nombre, "approved");
    }
}

/// <summary>Selecciona en tiempo de ejecución el proveedor activo (pantalla de demostración).</summary>
public sealed class SelectorPasarela
{
    private readonly IReadOnlyDictionary<string, IPasarelaPago> _proveedores;
    private readonly ConfiguracionDemo _demo;

    public SelectorPasarela(IEnumerable<IPasarelaPago> proveedores, ConfiguracionDemo demo)
    {
        _proveedores = proveedores.ToDictionary(p => p.Nombre, StringComparer.OrdinalIgnoreCase);
        _demo = demo;
    }

    public IPasarelaPago Actual =>
        _proveedores.TryGetValue(_demo.ProveedorPago, out var p) ? p : _proveedores[ConfiguracionDemo.ProveedorLegado];
}

/// <summary>Reembolso simulado: genera una referencia única y deja traza.</summary>
public sealed class ServicioReembolsoSimulado : IServicioReembolso
{
    private readonly IBitacoraTecnica _bitacora;

    public ServicioReembolsoSimulado(IBitacoraTecnica bitacora) => _bitacora = bitacora;

    public Task<string> ReembolsarAsync(int pedidoId, decimal monto, string referenciaPago, CancellationToken ct = default)
    {
        var referencia = $"RMB-{pedidoId:000000}-{Guid.NewGuid():N}"[..24];
        _bitacora.Registrar("REEMBOLSO", $"Reembolso simulado {referencia} de USD {Dinero.Formatear(monto)} sobre pago {referenciaPago}");
        return Task.FromResult(referencia);
    }
}
