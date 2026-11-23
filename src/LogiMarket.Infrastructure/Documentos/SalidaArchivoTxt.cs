using System.Text;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Documentos;

//implementador concreto del Bridge: escribe el comprobante en un archivo .txt real
public sealed class SalidaArchivoTxt : ISalidaDocumento
{
    public const string NombreMedio = "ArchivoTXT";
    private readonly string _carpeta;

    public SalidaArchivoTxt(string carpeta) => _carpeta = carpeta;

    public string Nombre => NombreMedio;

    public async Task<ResultadoSalida> EmitirAsync(string titulo, string contenido, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_carpeta);
        var nombre = string.Concat(titulo.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        var ruta = Path.Combine(_carpeta, $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
        await File.WriteAllTextAsync(ruta, contenido, Encoding.UTF8, ct);
        return new ResultadoSalida(true, Nombre, ruta, contenido.Length);
    }
}
