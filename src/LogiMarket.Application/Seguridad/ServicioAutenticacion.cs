using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Domain.Eventos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Seguridad;

public sealed class ServicioAutenticacion : IServicioAutenticacion
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IHasherContrasena _hasher;
    private readonly ISesionActual _sesion;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IBitacoraTecnica _bitacora;
    private readonly IReloj _reloj;

    public ServicioAutenticacion(IUsuarioRepositorio usuarios, IHasherContrasena hasher, ISesionActual sesion,
        IAuditoriaRepositorio auditoria, IUnidadDeTrabajo uow, IBitacoraTecnica bitacora, IReloj reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _sesion = sesion;
        _auditoria = auditoria;
        _uow = uow;
        _bitacora = bitacora;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion<UsuarioSesion>> IniciarSesionAsync(string nombreUsuario, string contrasena, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(contrasena))
            return ResultadoOperacion<UsuarioSesion>.Fallo("CREDENCIALES_REQUERIDAS", "Ingrese usuario y contraseña.");

        var usuario = await _usuarios.ObtenerPorNombreAsync(nombreUsuario.Trim().ToLowerInvariant(), ct);
        if (usuario is null || !usuario.Activo || !_hasher.Verificar(contrasena, usuario.HashContrasena))
        {
            _bitacora.Registrar("SEGURIDAD", $"LOGIN FALLIDO usuario={nombreUsuario}");
            await AuditarAsync(nombreUsuario, "LOGIN_FALLIDO", "Credenciales inválidas", ct);
            return ResultadoOperacion<UsuarioSesion>.Fallo("CREDENCIALES_INVALIDAS", "Usuario o contraseña incorrectos.");
        }

        var sesion = new UsuarioSesion(usuario.Id, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Rol);
        _sesion.Establecer(sesion);
        _bitacora.Registrar("SEGURIDAD", $"LOGIN usuario={usuario.NombreUsuario} rol={usuario.Rol}");
        await AuditarAsync(usuario.NombreUsuario, "LOGIN", $"Rol {usuario.Rol}", ct);
        return ResultadoOperacion<UsuarioSesion>.Ok(sesion, $"Bienvenido(a), {usuario.NombreCompleto}.");
    }

    public async Task CerrarSesionAsync(CancellationToken ct = default)
    {
        var nombre = _sesion.NombreUsuario;
        _sesion.Cerrar();
        _bitacora.Registrar("SEGURIDAD", $"LOGOUT usuario={nombre}");
        await AuditarAsync(nombre, "LOGOUT", "Cierre de sesión", ct);
    }

    private async Task AuditarAsync(string usuario, string accion, string detalle, CancellationToken ct)
    {
        _auditoria.Agregar(new Auditoria(_reloj.Ahora, usuario, "SEGURIDAD", accion, detalle));
        await _uow.GuardarCambiosAsync(ct);
    }
}
