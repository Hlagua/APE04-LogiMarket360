using LogiMarket.Application.Comun;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Logistica;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;

namespace LogiMarket.Application.Comandos;

/// <summary>Patrón Command: encapsula una petición (receptor + parámetros) como objeto.</summary>
public interface IComando
{
    string Nombre { get; }
    string Entrada { get; }
    Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default);
}

public sealed class ConfirmarPedidoCommand : IComando
{
    private readonly ConfirmacionPedidoFacade _receptor;
    public int PedidoId { get; }
    public string ClaveIdempotencia { get; }

    public ConfirmarPedidoCommand(ConfirmacionPedidoFacade receptor, int pedidoId, string claveIdempotencia)
    {
        _receptor = receptor;
        PedidoId = pedidoId;
        ClaveIdempotencia = claveIdempotencia;
    }

    public string Nombre => nameof(ConfirmarPedidoCommand);
    public string Entrada => $"pedido={PedidoId}; clave={ClaveIdempotencia}";
    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) =>
        await _receptor.ConfirmarAsync(PedidoId, ClaveIdempotencia, ct);

    /// <summary>Clave por defecto: una por pedido, de modo que un reintento no vuelve a cobrar.</summary>
    public static string ClavePorDefecto(int pedidoId) => $"CONF-{pedidoId:000000}";
}

public sealed class CancelarPedidoCommand : IComando
{
    private readonly ServicioPedidos _receptor;
    private readonly int _pedidoId;
    private readonly string _motivo;

    public CancelarPedidoCommand(ServicioPedidos receptor, int pedidoId, string motivo)
    {
        _receptor = receptor;
        _pedidoId = pedidoId;
        _motivo = motivo;
    }

    public string Nombre => nameof(CancelarPedidoCommand);
    public string Entrada => $"pedido={_pedidoId}; motivo={_motivo}";
    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => _receptor.CancelarAsync(_pedidoId, _motivo, ct);
}

public sealed class DespacharPedidoCommand : IComando
{
    private readonly ServicioLogistica _receptor;
    private readonly int _pedidoId;

    public DespacharPedidoCommand(ServicioLogistica receptor, int pedidoId)
    {
        _receptor = receptor;
        _pedidoId = pedidoId;
    }

    public string Nombre => nameof(DespacharPedidoCommand);
    public string Entrada => $"pedido={_pedidoId}";
    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => await _receptor.DespacharAsync(_pedidoId, ct);
}

public sealed class EntregarPedidoCommand : IComando
{
    private readonly ServicioLogistica _receptor;
    private readonly int _pedidoId;

    public EntregarPedidoCommand(ServicioLogistica receptor, int pedidoId)
    {
        _receptor = receptor;
        _pedidoId = pedidoId;
    }

    public string Nombre => nameof(EntregarPedidoCommand);
    public string Entrada => $"pedido={_pedidoId}";
    public Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => _receptor.EntregarAsync(_pedidoId, ct);
}

public sealed class ProcesarDevolucionCommand : IComando
{
    private readonly ServicioDevoluciones _receptor;
    private readonly SolicitudDevolucion _solicitud;

    public ProcesarDevolucionCommand(ServicioDevoluciones receptor, SolicitudDevolucion solicitud)
    {
        _receptor = receptor;
        _solicitud = solicitud;
    }

    public string Nombre => nameof(ProcesarDevolucionCommand);
    public string Entrada => $"pedido={_solicitud.PedidoId}; motivo={_solicitud.Motivo}; sinUso={_solicitud.ProductoSinUso}";
    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => await _receptor.SolicitarAsync(_solicitud, ct);
}

public sealed record RegistroComando(DateTime Fecha, string Usuario, string Comando, string Entrada, bool Exito, string Resultado);

/// <summary>
/// Invocador: ejecuta cualquier comando (botón de la UI o prueba automatizada), convierte excepciones en
/// resultados y registra comando, entrada y resultado. No conoce a los receptores.
/// </summary>
public sealed class InvocadorComandos
{
    private readonly List<RegistroComando> _historial = new();
    private readonly object _candado = new();
    private readonly ISesionActual _sesion;
    private readonly IBitacoraTecnica _bitacora;
    private readonly IReloj _reloj;

    public InvocadorComandos(ISesionActual sesion, IBitacoraTecnica bitacora, IReloj reloj)
    {
        _sesion = sesion;
        _bitacora = bitacora;
        _reloj = reloj;
    }

    public IReadOnlyList<RegistroComando> Historial
    {
        get { lock (_candado) return _historial.ToList(); }
    }

    public async Task<ResultadoOperacion> EjecutarAsync(IComando comando, CancellationToken ct = default)
    {
        ResultadoOperacion resultado;
        try
        {
            resultado = await comando.EjecutarAsync(ct);
        }
        catch (AccesoDenegadoException ex) { resultado = ResultadoOperacion.Fallo("ACCESO_DENEGADO", ex.Message); }
        catch (ExcepcionAplicacion ex) { resultado = ResultadoOperacion.Fallo(ex.Codigo, ex.Message); }
        catch (ExcepcionDominio ex) { resultado = ResultadoOperacion.Fallo(ex.Codigo, ex.Message); }

        var registro = new RegistroComando(_reloj.Ahora, _sesion.NombreUsuario, comando.Nombre, comando.Entrada,
            resultado.Exito, $"[{resultado.Codigo}] {resultado.Mensaje}");
        lock (_candado) _historial.Add(registro);
        _bitacora.Registrar("COMANDO", $"{registro.Comando}({registro.Entrada}) → {registro.Resultado}");
        return resultado;
    }
}
