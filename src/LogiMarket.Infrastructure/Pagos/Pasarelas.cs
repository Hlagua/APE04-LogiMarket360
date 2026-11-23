using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Infrastructure.Pagos;

//patron adapter (estructural)
//clase PasarelaLegadaAdapter
//objetivo: adapta CobrarCentavos al contrato CobrarAsync (decimal + clave) sin modificar el codigo legado
public sealed class PasarelaLegadaAdapter : IPasarelaPago
{
    private readonly PasarelaLegadaSimulada _legada;
    private readonly RegistroAutorizaciones _autorizaciones = new();

    public PasarelaLegadaAdapter(PasarelaLegadaSimulada legada) => _legada = legada;

    public string Nombre => ConfiguracionDemo.ProveedorLegado;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        ValidarEntrada(monto, claveIdempotencia);
        //el sistema legado no conoce claves de idempotencia: el Adapter añade esa garantia del contrato
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
        //precondiciones identicas en ambos proveedores (LSP)
        if (monto <= 0) throw new ArgumentOutOfRangeException(nameof(monto), "El monto debe ser positivo.");
        if (string.IsNullOrWhiteSpace(clave)) throw new ArgumentException("La clave de idempotencia es obligatoria.", nameof(clave));
    }
}

//segundo proveedor simulado, nativamente compatible y sustituible por DI
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

//selecciona en tiempo de ejecucion el proveedor activo (pantalla de demostracion)
public sealed class SelectorPasarela
{
    private readonly IReadOnlyDictionary<string, IPasarelaPago> _proveedores;
    private readonly ConfiguracionDemo _demo;

    public SelectorPasarela(IEnumerable<IPasarelaPago> proveedores, ConfiguracionDemo demo)
    {
        _proveedores = proveedores.ToDictionary(p => p.Nombre, StringComparer.OrdinalIgnoreCase);
        _demo = demo;
    }

    //un proveedor no registrado es un error de configuracion: no se elige otro en silencio
    public IPasarelaPago Actual =>
        _proveedores.TryGetValue(_demo.ProveedorPago, out var p)
            ? p
            : throw new ExcepcionAplicacion("PROVEEDOR_DESCONOCIDO", $"Proveedor de cobro no registrado: {_demo.ProveedorPago}");
}

//reembolso simulado: genera una referencia unica y deja traza
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

//idempotencia del lado del proveedor simulado (como las claves de idempotencia de una pasarela real):
//una clave ya AUTORIZADA devuelve la misma autorizacion sin volver a cobrar
//los rechazos no se recuerdan (decision documentada) para que el pedido pueda reintentarse con la
//misma clave
//reutilizar una clave con otro monto es un error
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
