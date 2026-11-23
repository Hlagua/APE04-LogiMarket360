using LogiMarket.Application.Comun;
using LogiMarket.Domain.Usuarios;

namespace LogiMarket.Application.Seguridad;

public sealed record UsuarioSesion(int Id, string NombreUsuario, string NombreCompleto, Rol Rol);

/// <summary>Sesión local actual (aplicación de escritorio de un solo usuario a la vez).</summary>
public interface ISesionActual
{
    UsuarioSesion? Usuario { get; }
    bool EstaAutenticado { get; }
    string NombreUsuario { get; }
    void Establecer(UsuarioSesion usuario);
    void Cerrar();
}

public sealed class SesionActual : ISesionActual
{
    private UsuarioSesion? _usuario;
    public UsuarioSesion? Usuario => _usuario;
    public bool EstaAutenticado => _usuario is not null;
    public string NombreUsuario => _usuario?.NombreUsuario ?? "anonimo";
    public void Establecer(UsuarioSesion usuario) => _usuario = usuario;
    public void Cerrar() => _usuario = null;
}

public enum Operacion
{
    ConsultarInformacion,
    GestionarCatalogo,
    AjustarInventario,
    GestionarClientes,
    GestionarBorradores,
    ValidarPedido,
    ConfirmarPedido,
    Cobrar,
    CancelarPedido,
    Despachar,
    Entregar,
    ProcesarDevolucion,
    EmitirComprobantes,
    VerAuditoria,
    AdministrarDemo
}

public interface IPoliticaPermisos
{
    bool Permite(Rol? rol, Operacion operacion);
    IEnumerable<Rol> RolesPara(Operacion operacion);
}

/// <summary>Matriz rol → operaciones. Es la política de seguridad real (no los botones de la UI).</summary>
public sealed class PoliticaPermisos : IPoliticaPermisos
{
    private static readonly Rol[] Todos = { Rol.ADMIN, Rol.VENTAS, Rol.BODEGA, Rol.CONSULTA };
    private static readonly Rol[] Ventas = { Rol.ADMIN, Rol.VENTAS };
    private static readonly Rol[] Bodega = { Rol.ADMIN, Rol.BODEGA };
    private static readonly Rol[] Admin = { Rol.ADMIN };

    private readonly IReadOnlyDictionary<Operacion, Rol[]> _matriz = new Dictionary<Operacion, Rol[]>
    {
        [Operacion.ConsultarInformacion] = Todos,
        [Operacion.EmitirComprobantes] = Todos,
        [Operacion.VerAuditoria] = Todos,
        [Operacion.GestionarCatalogo] = Admin,
        [Operacion.AjustarInventario] = Admin,
        [Operacion.AdministrarDemo] = Admin,
        [Operacion.GestionarClientes] = Ventas,
        [Operacion.GestionarBorradores] = Ventas,
        [Operacion.ValidarPedido] = Ventas,
        [Operacion.ConfirmarPedido] = Ventas,
        [Operacion.Cobrar] = Ventas,
        [Operacion.CancelarPedido] = Ventas,
        [Operacion.ProcesarDevolucion] = Ventas,
        [Operacion.Despachar] = Bodega,
        [Operacion.Entregar] = Bodega
    };

    public bool Permite(Rol? rol, Operacion operacion) =>
        rol is not null && _matriz.TryGetValue(operacion, out var roles) && roles.Contains(rol.Value);

    public IEnumerable<Rol> RolesPara(Operacion operacion) => _matriz[operacion];
}

/// <summary>Punto único de autorización de los casos de uso (servicios protegidos).</summary>
public interface IServicioAutorizacion
{
    bool Puede(Operacion operacion);
    void Exigir(Operacion operacion);
}

public sealed class ServicioAutorizacion : IServicioAutorizacion
{
    private readonly ISesionActual _sesion;
    private readonly IPoliticaPermisos _politica;
    private readonly IBitacoraTecnica _bitacora;
    private readonly Metricas.IMetricasSesion _metricas;

    public ServicioAutorizacion(ISesionActual sesion, IPoliticaPermisos politica, IBitacoraTecnica bitacora, Metricas.IMetricasSesion metricas)
    {
        _sesion = sesion;
        _politica = politica;
        _bitacora = bitacora;
        _metricas = metricas;
    }

    public bool Puede(Operacion operacion) => _politica.Permite(_sesion.Usuario?.Rol, operacion);

    public void Exigir(Operacion operacion)
    {
        if (!_sesion.EstaAutenticado)
            throw Denegar(operacion, "Debe iniciar sesión.");
        if (!Puede(operacion))
            throw Denegar(operacion, $"El rol {_sesion.Usuario!.Rol} no tiene permiso para {operacion}.");
    }

    private AccesoDenegadoException Denegar(Operacion operacion, string mensaje)
    {
        _bitacora.Registrar("SEGURIDAD", $"DENEGADO {operacion} usuario={_sesion.NombreUsuario}: {mensaje}");
        _metricas.RegistrarDenegacion();
        return new AccesoDenegadoException(operacion.ToString(), mensaje);
    }
}

/// <summary>Puerto de hash de contraseñas (implementado con PBKDF2 en Infrastructure).</summary>
public interface IHasherContrasena
{
    string Hashear(string contrasena);
    bool Verificar(string contrasena, string hash);
}
