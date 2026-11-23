using LogiMarket.Application.Comun;
using LogiMarket.Application.Reportes;
using LogiMarket.Domain.Comun;
using Microsoft.Extensions.DependencyInjection;

namespace LogiMarket.WinForms.Infraestructura;

//ejecuta cada accion de la UI en un ambito DI nuevo (un DbContext / Unidad de Trabajo por operacion)
//traduce las excepciones esperadas a mensajes claros
//no contiene reglas de negocio
public sealed class Ejecutor
{
    private readonly IServiceProvider _raiz;

    public Ejecutor(IServiceProvider raiz) => _raiz = raiz;

    //mensajes de estado (lo muestra la barra inferior del formulario principal)
    public event Action<string, bool>? Mensaje;

    public T Singleton<T>() where T : notnull => _raiz.GetRequiredService<T>();

    public async Task<(bool Ok, T? Valor)> Ejecutar<T>(Func<IServiceProvider, Task<T>> accion)
    {
        using var scope = _raiz.CreateScope();
        try
        {
            return (true, await accion(scope.ServiceProvider));
        }
        catch (Exception ex) when (EsEsperada(ex))
        {
            Informar(ex);
            return (false, default);
        }
    }

    public async Task<bool> Ejecutar(Func<IServiceProvider, Task> accion)
    {
        var (ok, _) = await Ejecutar<bool>(async sp => { await accion(sp); return true; });
        return ok;
    }

    //muestra el resultado de una operacion de aplicacion
    public bool Mostrar(ResultadoOperacion? r, bool dialogoEnExito = false)
    {
        if (r is null) return false;
        Mensaje?.Invoke(r.Mensaje, r.Exito);
        if (!r.Exito)
            MessageBox.Show(r.Mensaje, $"Operación rechazada [{r.Codigo}]", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (dialogoEnExito)
            MessageBox.Show(r.Mensaje, "Operación exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return r.Exito;
    }

    public void Estado(string texto) => Mensaje?.Invoke(texto, true);

    private static bool EsEsperada(Exception ex) =>
        ex is AccesoDenegadoException or ExcepcionAplicacion or ExcepcionDominio or ExcepcionSintaxisFiltro
            or InvalidOperationException or ArgumentException or IOException;

    private void Informar(Exception ex)
    {
        var (titulo, icono) = ex switch
        {
            AccesoDenegadoException => ("Acceso denegado", MessageBoxIcon.Stop),
            ExcepcionDominio d => ($"Regla de negocio [{d.Codigo}]", MessageBoxIcon.Warning),
            ExcepcionAplicacion a => ($"Operación rechazada [{a.Codigo}]", MessageBoxIcon.Warning),
            ExcepcionSintaxisFiltro => ("Filtro inválido", MessageBoxIcon.Warning),
            _ => ("Error", MessageBoxIcon.Error)
        };
        Mensaje?.Invoke(ex.Message, false);
        MessageBox.Show(ex.Message, titulo, MessageBoxButtons.OK, icono);
    }
}
