using System.Runtime.CompilerServices;
using System.Text;

namespace LogiMarket.Tests.Soporte;

/// <summary>
/// Registro de trazabilidad de las pruebas: por cada caso guarda entrada, resultado ESPERADO, resultado
/// OBTENIDO, fecha, archivo y método. Se escribe en bin/.../evidencias/matriz-esperado-obtenido.md al correr
/// `dotnet test` y sirve como evidencia (log) de la matriz de 38 pruebas.
/// </summary>
public static class Evidencia
{
    private static readonly object Candado = new();
    private static readonly string Ruta = Path.Combine(AppContext.BaseDirectory, "evidencias", "matriz-esperado-obtenido.md");
    private static bool _iniciado;

    public static string RutaArchivo => Ruta;

    /// <summary>Registra el caso y falla la prueba si lo obtenido no coincide con lo esperado.</summary>
    public static void Registrar(string caso, string entrada, string esperado, string obtenido,
        [CallerMemberName] string metodo = "", [CallerFilePath] string archivo = "")
    {
        var coincide = string.Equals(esperado, obtenido, StringComparison.Ordinal);
        lock (Candado)
        {
            if (!_iniciado)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
                File.WriteAllText(Ruta,
                    "# Matriz de pruebas automatizadas: esperado vs. obtenido\n\n" +
                    $"Ejecución: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                    "| Caso | Entrada | Esperado | Obtenido | Resultado | Archivo / método |\n" +
                    "|---|---|---|---|---|---|\n", Encoding.UTF8);
                _iniciado = true;
            }
            File.AppendAllText(Ruta,
                $"| {caso} | {Celda(entrada)} | {Celda(esperado)} | {Celda(obtenido)} | {(coincide ? "PASA" : "FALLA")} | " +
                $"{Path.GetFileName(archivo)} / {metodo} |\n", Encoding.UTF8);
        }
        Assert.Equal(esperado, obtenido);
    }

    private static string Celda(string texto) => texto.Replace("|", "/").Replace("\n", " ");
}
