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
    private readonly RegistroAutorizaciones _autorizaciones = new();

    public PasarelaLegadaAdapter(PasarelaLegadaSimulada legada) => _legada = legada;

    public string Nombre => ConfiguracionDemo.ProveedorLegado;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        ValidarEntrada(monto, claveIdempotencia);
        // El sistema legado no conoce claves de idempotencia: el Adapter añade esa garantía del contrato.
        var resultado = _autorizaciones.Obtener(claveIdempotencia, monto, () =>
        {
            var centavos = (long)(Dinero.Redondear(monto) * 100m);
            var respuesta = _legada.CobrarCentavos(centavos, claveIdempotencia);
            var aprobado = respuesta.Codigo == "00";
            return new ResultadoCobro(aprobado, aprobado ? respuesta.NumeroAutorizacion : $"RECH-{respuesta.Codigo}",
                Nombre, $"{respuesta.Codigo} {respuesta.Texto}");
        });
        return Task.FromResult(resultado);
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
    private readonly RegistroAutorizaciones _autorizaciones = new();
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
        await Task.Yield();
        return _autorizaciones.Obtener(claveIdempotencia, monto, () => Autorizar(monto, claveIdempotencia));
    }

    private ResultadoCobro Autorizar(decimal monto, string claveIdempotencia)
    {
        _bitacora.Registrar("PAGO", $"PASARELA MODERNA charge(amount={Dinero.Formatear(monto)}, idempotencyKey={claveIdempotencia})");
        if (_demo.ForzarErrorPasarela) throw new TimeoutException("La pasarela moderna no respondió (simulado).");
        if (_demo.ForzarRechazoCobro || monto > ParametrosNegocio.MontoMaximoCobroSimulado)
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

    /// <summary>Un proveedor no registrado es un error de configuración: no se elige otro en silencio.</summary>
    public IPasarelaPago Actual =>
        _proveedores.TryGetValue(_demo.ProveedorPago, out var p)
            ? p
            : throw new ExcepcionAplicacion("PROVEEDOR_DESCONOCIDO", $"Proveedor de cobro no registrado: {_demo.ProveedorPago}");
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

/// <summary>
/// Idempotencia del lado del proveedor simulado (como las claves de idempotencia de una pasarela real):
/// una clave ya AUTORIZADA devuelve la misma autorización sin volver a cobrar. Los rechazos no se recuerdan
/// (decisión documentada) para que el pedido pueda reintentarse con la misma clave. Reutilizar una clave
/// con otro monto es un error.
/// </summary>
internal sealed class RegistroAutorizaciones
{
    private readonly Dictionary<string, (decimal Monto, ResultadoCobro Resultado)> _aprobadas = new();
    private readonly object _candado = new();

    public ResultadoCobro Obtener(string clave, decimal monto, Func<ResultadoCobro> autorizar)
    {
        lock (_candado)
        {
            if (_aprobadas.TryGetValue(clave, out var previa))
            {
                if (previa.Monto != monto)
                    throw new ArgumentException($"La clave {clave} ya se usó con otro monto ({Dinero.Formatear(previa.Monto)}).", nameof(clave));
                return previa.Resultado with { Mensaje = previa.Resultado.Mensaje + " (repetición idempotente: sin nuevo cobro)" };
            }
            var resultado = autorizar();
            if (resultado.Aprobado) _aprobadas[clave] = (monto, resultado);
            return resultado;
        }
    }
}
