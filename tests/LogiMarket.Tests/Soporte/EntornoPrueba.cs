using LogiMarket.Application.Comandos;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Infrastructure;
using LogiMarket.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Tests.Soporte;

//reloj controlable para simular dias transcurridos
public sealed class RelojFijo : IReloj
{
    public DateTime Ahora { get; set; } = new(2026, 11, 1, 10, 0, 0);
    public void AvanzarDias(int dias) => Ahora = Ahora.AddDays(dias);
}

//base de pruebas AISLADA:
//un archivo SQLite nuevo por prueba, migrado y sembrado con los datos controlados de la guia
//usa la misma composicion de dependencias que la aplicacion real
public sealed class EntornoPrueba : IAsyncDisposable
{
    private readonly string _carpeta;
    public ServiceProvider Proveedor { get; }
    public RelojFijo Reloj { get; } = new();
    public string RutaBaseDatos { get; }

    private EntornoPrueba(Action<IServiceCollection>? personalizar)
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "logimarket-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        RutaBaseDatos = Path.Combine(_carpeta, "prueba.db");
        var servicios = new ServiceCollection();
        servicios.AgregarLogiMarket(new OpcionesLogiMarket
        {
            RutaBaseDatos = RutaBaseDatos,
            CarpetaSalidas = Path.Combine(_carpeta, "salidas"),
            CarpetaLogs = null
        });
        servicios.AddSingleton<IReloj>(Reloj);
        personalizar?.Invoke(servicios);
        Proveedor = servicios.BuildServiceProvider();
    }

    public static async Task<EntornoPrueba> CrearAsync(Action<IServiceCollection>? personalizar = null)
    {
        Application.Metricas.MetricasSesion.Instancia.Reiniciar(); //singleton: contadores limpios por prueba
        var e = new EntornoPrueba(personalizar);
        using var scope = e.Proveedor.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IInicializadorBaseDatos>().InicializarAsync();
        return e;
    }

    public string CarpetaSalidas => Path.Combine(_carpeta, "salidas");

    public T Unico<T>() where T : notnull => Proveedor.GetRequiredService<T>();

    //ejecuta una operacion en un ambito nuevo (igual que cada accion de la UI)
    public async Task<TR> Usar<TS, TR>(Func<TS, Task<TR>> accion) where TS : notnull
    {
        using var scope = Proveedor.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<TS>());
    }

    public async Task<TR> Db<TR>(Func<LogiMarketDbContext, Task<TR>> consulta)
    {
        using var scope = Proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogiMarketDbContext>();
        return await consulta(db);
    }

    public async Task IniciarSesion(string usuario)
    {
        var clave = InicializadorBaseDatos.UsuariosDemo.Single(u => u.Usuario == usuario).Clave;
        var r = await Usar<IServicioAutenticacion, ResultadoOperacion<UsuarioSesion>>(s => s.IniciarSesionAsync(usuario, clave));
        Assert.True(r.Exito, r.Mensaje);
    }

    public async Task<int> Stock(string sku) => await Db(db => db.Productos.Where(p => p.Sku == sku).Select(p => p.Stock).SingleAsync());

    public async Task<int> ClienteId(string nombre) => await Db(db => db.Clientes.Where(c => c.Nombre == nombre).Select(c => c.Id).SingleAsync());

    //crea y guarda un borrador con las lineas indicadas (como lo haria el carrito)
    public async Task<int> CrearBorrador(string cliente, TipoEntrega entrega, params (string Codigo, int Cantidad)[] lineas)
    {
        using var scope = Proveedor.CreateScope();
        var sp = scope.ServiceProvider;
        var pedidos = sp.GetRequiredService<IServicioPedidos>();
        var consultas = sp.GetRequiredService<IServicioConsultas>();
        var editor = pedidos.NuevoEditor();
        editor.SeleccionarCliente(await consultas.ClienteAsync(await ClienteId(cliente)));
        editor.CambiarEntrega(entrega);
        foreach (var (codigo, cantidad) in lineas) editor.Agregar(await consultas.ElementoAsync(codigo), cantidad);
        var r = await pedidos.GuardarBorradorAsync(editor);
        Assert.True(r.Exito, r.Mensaje);
        return r.Valor;
    }

    public async Task<string> ClaveDefecto(int pedidoId) =>
        ConfirmarPedidoCommand.ClavePorDefecto(pedidoId, await Db(db => db.Pedidos.Where(p => p.Id == pedidoId).Select(p => p.FechaCreacion).SingleAsync()));

    public async Task<ResultadoConfirmacion> Confirmar(int pedidoId, string? clave = null)
    {
        var k = clave ?? await ClaveDefecto(pedidoId);
        return await Usar<IConfirmacionPedido, ResultadoConfirmacion>(f => f.ConfirmarAsync(pedidoId, k));
    }

    public async ValueTask DisposeAsync()
    {
        await Proveedor.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
