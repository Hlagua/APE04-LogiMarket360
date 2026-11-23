using LogiMarket.Application.Comun;
using LogiMarket.Application.Devoluciones;
using LogiMarket.Application.Logistica;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Comun;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Comandos;

//patron command (comportamental)
//interfaz IComando
//objetivo: encapsula una peticion (receptor + parametros) como objeto
public interface IComando
{
    string Nombre { get; }
    string Entrada { get; }
    Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default);
}

public sealed class ConfirmarPedidoCommand : IComando
{
    private readonly IConfirmacionPedido _receptor;
    public int PedidoId { get; }
    public string ClaveIdempotencia { get; }

    public ConfirmarPedidoCommand(IConfirmacionPedido receptor, int pedidoId, string claveIdempotencia)
    {
        _receptor = receptor;
        PedidoId = pedidoId;
        ClaveIdempotencia = claveIdempotencia;
    }

    public string Nombre => nameof(ConfirmarPedidoCommand);
    public string Entrada => $"pedido={PedidoId}; clave={ClaveIdempotencia}";
    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) =>
        await _receptor.ConfirmarAsync(PedidoId, ClaveIdempotencia, ct);

    //clave por defecto: una por pedido (un reintento no vuelve a cobrar)
    //incluye la fecha de creacion para que, tras restablecer la base de pruebas y reutilizarse los ids
    //un pedido nuevo no herede la autorizacion que la pasarela recuerda para un pedido anterior con el mismo numero
    public static string ClavePorDefecto(int pedidoId, DateTime fechaCreacion) =>
        $"CONF-{pedidoId:000000}-{fechaCreacion:yyyyMMddHHmmssfff}";
}

public sealed class CancelarPedidoCommand : IComando
{
    private readonly IServicioPedidos _receptor;
    private readonly int _pedidoId;
    private readonly string _motivo;

    public CancelarPedidoCommand(IServicioPedidos receptor, int pedidoId, string motivo)
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
    private readonly IServicioLogistica _receptor;
    private readonly int _pedidoId;

    public DespacharPedidoCommand(IServicioLogistica receptor, int pedidoId)
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
    private readonly IServicioLogistica _receptor;
    private readonly int _pedidoId;

    public EntregarPedidoCommand(IServicioLogistica receptor, int pedidoId)
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
    private readonly IServicioDevoluciones _receptor;
    private readonly SolicitudDevolucion _solicitud;

    public ProcesarDevolucionCommand(IServicioDevoluciones receptor, SolicitudDevolucion solicitud)
    {
        _receptor = receptor;
        _solicitud = solicitud;
    }

    public string Nombre => nameof(ProcesarDevolucionCommand);
    public string Entrada => $"pedido={_solicitud.PedidoId}; motivo={_solicitud.Motivo}; sinUso={_solicitud.ProductoSinUso}";
    public async Task<ResultadoOperacion> EjecutarAsync(CancellationToken ct = default) => await _receptor.SolicitarAsync(_solicitud, ct);
}

public sealed record RegistroComando(DateTime Fecha, string Usuario, string Comando, string Entrada, bool Exito, string Resultado);

//invocador: ejecuta cualquier comando (boton de la UI o prueba automatizada)
//convierte excepciones en resultados y registra comando, entrada y resultado
//no conoce a los receptores
public sealed class InvocadorComandos : IInvocadorComandos
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
