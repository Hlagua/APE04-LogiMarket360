using LogiMarket.Application.Comun;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Application.Pagos;

//patron proxy (estructural)
//clase PagoAutorizadoProxy
//tipo: proxy de proteccion
//objetivo: verifica el rol (VENTAS/ADMIN) ANTES de delegar
//si se deniega, no se invoca al Decorator ni a la pasarela y por tanto no hay LOG de cobro
//solo un registro de SEGURIDAD
//funciona sin WinForms (la politica vive en Application)
public sealed class PagoAutorizadoProxy : IPasarelaPago
{
    private readonly IPasarelaPago _real;
    private readonly ISesionActual _sesion;
    private readonly IPoliticaPermisos _permisos;
    private readonly IBitacoraTecnica _bitacora;
    private readonly IMetricasSesion _metricas;

    public PagoAutorizadoProxy(IPasarelaPago real, ISesionActual sesion, IPoliticaPermisos permisos,
        IBitacoraTecnica bitacora, IMetricasSesion metricas)
    {
        _real = real;
        _sesion = sesion;
        _permisos = permisos;
        _bitacora = bitacora;
        _metricas = metricas;
    }

    public string Nombre => _real.Nombre;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, CancellationToken ct = default)
    {
        var rol = _sesion.Usuario?.Rol;
        if (!_permisos.Permite(rol, Operacion.Cobrar))
        {
            _bitacora.Registrar("SEGURIDAD",
                $"PROXY COBRO DENEGADO usuario={_sesion.NombreUsuario} rol={rol?.ToString() ?? "-"} clave={claveIdempotencia}");
            _metricas.RegistrarDenegacion();
            throw new AccesoDenegadoException(nameof(Operacion.Cobrar),
                $"El rol {rol?.ToString() ?? "anónimo"} no está autorizado para cobrar.");
        }
        return _real.CobrarAsync(monto, claveIdempotencia, ct);
    }
}
