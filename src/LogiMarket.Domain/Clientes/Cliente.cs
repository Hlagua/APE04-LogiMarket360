using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Clientes;

public class Cliente
{
    public int Id { get; private set; }
    public string Identificacion { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public CategoriaCliente Categoria { get; private set; }
    public bool Activo { get; private set; }

    private Cliente() { }

    public static Cliente Crear(string identificacion, string nombre, string email, string telefono, CategoriaCliente categoria)
    {
        var c = new Cliente { Activo = true };
        c.Identificacion = Guardia.Texto(identificacion, "identificación", 20);
        c.Actualizar(nombre, email, telefono, categoria);
        return c;
    }

    public void Actualizar(string nombre, string email, string telefono, CategoriaCliente categoria)
    {
        Nombre = Guardia.Texto(nombre, "nombre", 120);
        var correo = Guardia.Texto(email, "email", 120);
        if (!correo.Contains('@') || correo.StartsWith('@') || correo.EndsWith('@'))
            throw new ExcepcionDominio("EMAIL_INVALIDO", "El email no tiene un formato válido.");
        Email = correo;
        Telefono = (telefono ?? string.Empty).Trim();
        Categoria = categoria;
    }

    public void Activar() => Activo = true;
    public void Desactivar() => Activo = false;
}
