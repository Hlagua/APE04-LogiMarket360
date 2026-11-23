using LogiMarket.Application.Comun;

namespace LogiMarket.Infrastructure.Registro;

//bitacora tecnica en memoria + archivo de texto (logs/bitacora-AAAAMMDD.log)
//independiente de las transacciones de base de datos
//segura para hilos
public sealed class BitacoraArchivo : IBitacoraTecnica
{
    private readonly List<EntradaBitacora> _entradas = new();
    private readonly object _candado = new();
    private readonly string? _carpeta;

    public BitacoraArchivo(string? carpeta)
    {
        _carpeta = carpeta;
        if (_carpeta is not null) Directory.CreateDirectory(_carpeta);
    }

    public void Registrar(string categoria, string mensaje)
    {
        var entrada = new EntradaBitacora(DateTime.Now, categoria, mensaje, ContextoCorrelacion.Actual);
        lock (_candado)
        {
            _entradas.Add(entrada);
            if (_carpeta is null) return;
            try
            {
                File.AppendAllText(Path.Combine(_carpeta, $"bitacora-{entrada.Fecha:yyyyMMdd}.log"), entrada + Environment.NewLine);
            }
            catch (IOException)
            {
                //el log en archivo es complementario
                //la entrada sigue disponible en memoria
            }
        }
    }

    public IReadOnlyList<EntradaBitacora> Entradas(string? categoria = null)
    {
        lock (_candado)
            return categoria is null ? _entradas.ToList() : _entradas.Where(e => e.Categoria == categoria).ToList();
    }
}
