using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Application.Pagos;

//patron decorator (estructural)
//clase AuditoriaPagoDecorator
//objetivo: envuelve cualquier IPasarelaPago para registrar INICIO, y FIN o ERROR
//con un identificador de correlacion, SIN alterar el resultado del cobro concreto
public sealed class AuditoriaPagoDecorator : IPasarelaPago
{
    public const string Categoria = "PAGO";
    private readonly IPasarelaPago _interna;
    private readonly IBitacoraTecnica _bitacora;

    public AuditoriaPagoDecorator(IPasarelaPago interna, IBitacoraTecnica bitacora)
    {
        _interna = interna;
        _bitacora = bitacora;
    }

    public string Nombre => _interna.Nombre;

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        var correlacion = Guid.NewGuid().ToString("N")[..12];
        using var _ = ContextoCorrelacion.Iniciar(correlacion);
        _bitacora.Registrar(Categoria, $"INICIO cobro proveedor={_interna.Nombre} monto={Domain.Comun.Dinero.Formatear(monto)} clave={claveIdempotencia}");
        try
        {
            var resultado = await _interna.CobrarAsync(monto, claveIdempotencia, ct);
            _bitacora.Registrar(Categoria,
                $"FIN cobro aprobado={resultado.Aprobado} ref={resultado.Referencia} msg={resultado.Mensaje}");
            return resultado with { CorrelacionId = correlacion };
        }
        catch (Exception ex)
        {
            _bitacora.Registrar(Categoria, $"ERROR cobro {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
