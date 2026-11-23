using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Usuarios;

//cuenta local
//solo guarda el hash de la contraseña, nunca el texto claro
public class Usuario
{
    public int Id { get; private set; }
    public string NombreUsuario { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;
    public Rol Rol { get; private set; }
    public string HashContrasena { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    private Usuario() { }

    public static Usuario Crear(string nombreUsuario, string nombreCompleto, Rol rol, string hashContrasena)
    {
        return new Usuario
        {
            NombreUsuario = Guardia.Texto(nombreUsuario, "usuario", 50).ToLowerInvariant(),
            NombreCompleto = Guardia.Texto(nombreCompleto, "nombre completo", 120),
            Rol = rol,
            HashContrasena = Guardia.Texto(hashContrasena, "hash", 300),
            Activo = true
        };
    }
}
