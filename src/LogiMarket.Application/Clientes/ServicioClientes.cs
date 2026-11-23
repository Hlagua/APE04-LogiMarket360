using LogiMarket.Application.Comun;
using LogiMarket.Application.Contratos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Eventos;

namespace LogiMarket.Application.Clientes;

public sealed record ClienteEdicion(int Id, string Identificacion, string Nombre, string Email, string Telefono, CategoriaCliente Categoria);

/// <summary>Comandos de clientes. Las consultas están en ServicioConsultas.</summary>
public sealed class ServicioClientes : IServicioClientes
{
    private readonly IClienteRepositorio _clientes;
    private readonly IAuditoriaRepositorio _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly ISesionActual _sesion;
    private readonly IReloj _reloj;

    public ServicioClientes(IClienteRepositorio clientes, IAuditoriaRepositorio auditoria,
        IUnidadDeTrabajo uow, IServicioAutorizacion autorizacion, ISesionActual sesion, IReloj reloj)
    {
        _clientes = clientes;
        _auditoria = auditoria;
        _uow = uow;
        _autorizacion = autorizacion;
        _sesion = sesion;
        _reloj = reloj;
    }

    public async Task<ResultadoOperacion<int>> GuardarAsync(ClienteEdicion datos, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarClientes);
        Cliente cliente;
        if (datos.Id == 0)
        {
            cliente = Cliente.Crear(datos.Identificacion, datos.Nombre, datos.Email, datos.Telefono, datos.Categoria);
            if (await _clientes.ExisteIdentificacionAsync(cliente.Identificacion, ct))
                return ResultadoOperacion<int>.Fallo("CLIENTE_DUPLICADO", $"Ya existe un cliente con identificación {cliente.Identificacion}.");
            _clientes.Agregar(cliente);
        }
        else
        {
            cliente = await _clientes.ObtenerAsync(datos.Id, ct)
                      ?? throw new ExcepcionAplicacion("CLIENTE_NO_EXISTE", $"No existe el cliente {datos.Id}.");
            cliente.Actualizar(datos.Nombre, datos.Email, datos.Telefono, datos.Categoria);
        }
        _auditoria.Agregar(new Auditoria(_reloj.Ahora, _sesion.NombreUsuario, "CLIENTES",
            datos.Id == 0 ? "CLIENTE_CREADO" : "CLIENTE_ACTUALIZADO", $"{cliente.Nombre} ({cliente.Categoria})"));
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion<int>.Ok(cliente.Id, $"Cliente {cliente.Nombre} guardado.");
    }

    public async Task<ResultadoOperacion> CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.GestionarClientes);
        var cliente = await _clientes.ObtenerAsync(id, ct)
                      ?? throw new ExcepcionAplicacion("CLIENTE_NO_EXISTE", $"No existe el cliente {id}.");
        if (activo) cliente.Activar(); else cliente.Desactivar();
        await _uow.GuardarCambiosAsync(ct);
        return ResultadoOperacion.Ok($"Cliente {cliente.Nombre} {(activo ? "activado" : "desactivado")}.");
    }
}
