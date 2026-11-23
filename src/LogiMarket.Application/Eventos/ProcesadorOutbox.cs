using LogiMarket.Application.Comun;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Eventos;

public sealed record ResultadoProcesoOutbox(int Procesados, int Fallidos, IReadOnlyList<string> Detalle);

//procesa los eventos pendientes DESPUES de confirmar la transaccion de negocio:
//los publica a los observadores y los marca procesados
//si un observador falla
//el evento queda pendiente (con intentos y error) y se recupera en una ejecucion posterior
//un candado global evita el procesamiento duplicado
public sealed class ProcesadorOutbox : IProcesadorOutbox
{
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private readonly IOutboxRepositorio _outbox;
    private readonly IPublicadorEventos _publicador;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IReloj _reloj;
    private readonly IBitacoraTecnica _bitacora;

    public ProcesadorOutbox(IOutboxRepositorio outbox, IPublicadorEventos publicador, IUnidadDeTrabajo uow,
        IReloj reloj, IBitacoraTecnica bitacora)
    {
        _outbox = outbox;
        _publicador = publicador;
        _uow = uow;
        _reloj = reloj;
        _bitacora = bitacora;
    }

    public async Task<ResultadoProcesoOutbox> ProcesarPendientesAsync(int maximo = 50, CancellationToken ct = default)
    {
        await Candado.WaitAsync(ct);
        try
        {
            var detalle = new List<string>();
            int ok = 0, fallos = 0;
            var pendientes = (await _outbox.PendientesAsync(maximo, ct)).Select(e => e.Id).ToList();
            foreach (var id in pendientes)
            {
                //se recarga cada evento: tras un fallo anterior el contexto pudo haberse limpiado
                var registro = await _outbox.ObtenerAsync(id, ct);
                if (registro is null || registro.Procesado) continue;
                try
                {
                    var evento = SerializadorEventos.DesdeOutbox(registro);
                    await _publicador.PublicarAsync(evento, ct);
                    registro.MarcarProcesado(_reloj.Ahora);
                    await _uow.GuardarCambiosAsync(ct);
                    ok++;
                    detalle.Add($"Evento {registro.Id} {registro.Tipo} procesado");
                    _bitacora.Registrar("OUTBOX", $"Evento {registro.Id} {registro.Tipo} procesado (intentos={registro.Intentos})");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fallos++;
                    _uow.DescartarCambios();
                    var recargado = await _outbox.ObtenerAsync(registro.Id, ct);
                    recargado?.RegistrarFallo(ex.Message);
                    await _uow.GuardarCambiosAsync(ct);
                    detalle.Add($"Evento {registro.Id} {registro.Tipo} PENDIENTE: {ex.Message}");
                    _bitacora.Registrar("OUTBOX", $"Evento {registro.Id} {registro.Tipo} falló y queda pendiente: {ex.Message}");
                }
            }
            return new ResultadoProcesoOutbox(ok, fallos, detalle);
        }
        finally
        {
            Candado.Release();
        }
    }

    //version que nunca propaga errores (se usa justo despues de confirmar una operacion)
    public async Task<ResultadoProcesoOutbox> ProcesarSinPropagarAsync(CancellationToken ct = default)
    {
        try
        {
            return await ProcesarPendientesAsync(50, ct);
        }
        catch (Exception ex)
        {
            _bitacora.Registrar("OUTBOX", $"Procesamiento diferido por error: {ex.Message}");
            return new ResultadoProcesoOutbox(0, 0, new[] { ex.Message });
        }
    }
}
