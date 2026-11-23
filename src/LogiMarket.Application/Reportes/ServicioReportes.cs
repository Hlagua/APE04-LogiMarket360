using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comun;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Reportes;

public sealed record ResultadoFiltro(string Arbol, IReadOnlyList<PedidoResumenDto> Pedidos);

public enum TipoComprobante { Venta, Devolucion }

/// <summary>Reportes: filtro con Interpreter, comprobantes con Bridge y vista masiva con Flyweight.</summary>
public sealed class ServicioReportes : IServicioReportes
{
    private readonly IConsultasLectura _consultas;
    private readonly IServicioAutorizacion _autorizacion;
    private readonly IReadOnlyDictionary<string, ISalidaDocumento> _salidas;
    private readonly IGeneradorVistaMasiva _vistaMasiva;

    public ServicioReportes(IConsultasLectura consultas, IServicioAutorizacion autorizacion,
        IEnumerable<ISalidaDocumento> salidas, IGeneradorVistaMasiva vistaMasiva)
    {
        _consultas = consultas;
        _autorizacion = autorizacion;
        _salidas = salidas.ToDictionary(s => s.Nombre, StringComparer.OrdinalIgnoreCase);
        _vistaMasiva = vistaMasiva;
    }

    public IEnumerable<string> MediosDisponibles => _salidas.Keys;

    /// <summary>Interpreter: analiza el texto, construye el árbol y lo evalúa sobre los pedidos.</summary>
    public async Task<ResultadoFiltro> FiltrarPedidosAsync(string filtro, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var expresion = AnalizadorFiltro.Analizar(filtro); // lanza ExcepcionSintaxisFiltro si es inválido
        var pedidos = await _consultas.PedidosAsync(ct);
        return new ResultadoFiltro(expresion.Describir(), pedidos.Where(expresion.Interpretar).ToList());
    }

    /// <summary>Bridge: combina el tipo de comprobante con el medio de salida elegido.</summary>
    public async Task<ResultadoSalida> EmitirComprobanteAsync(TipoComprobante tipo, int pedidoId, string medio, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.EmitirComprobantes);
        if (!_salidas.TryGetValue(medio, out var salida))
            throw new ExcepcionAplicacion("MEDIO_DESCONOCIDO", $"Medio de salida no disponible: {medio}");
        var pedido = await _consultas.PedidoAsync(pedidoId, ct)
                     ?? throw new ExcepcionAplicacion("PEDIDO_NO_EXISTE", $"No existe el pedido {pedidoId}.");

        Comprobante comprobante = tipo switch
        {
            TipoComprobante.Venta when pedido.Pago is null =>
                throw new ExcepcionAplicacion("SIN_PAGO", "Solo se emite comprobante de venta para pedidos pagados."),
            TipoComprobante.Venta => new ComprobanteVenta(pedido, salida),
            TipoComprobante.Devolucion when pedido.Devolucion is null =>
                throw new ExcepcionAplicacion("SIN_DEVOLUCION", "El pedido no tiene una devolución registrada."),
            _ => new ComprobanteDevolucion(pedido, salida)
        };
        return await comprobante.EmitirAsync(ct);
    }

    public async Task<(IReadOnlyList<FilaCatalogoVisual> Filas, MedicionFlyweight Medicion)> VistaMasivaAsync(int filas = 1000, CancellationToken ct = default)
    {
        _autorizacion.Exigir(Operacion.ConsultarInformacion);
        var productos = await _consultas.ProductosAsync(null, ct);
        var medicion = _vistaMasiva.Medir(productos, filas);
        return (_vistaMasiva.GenerarConFlyweight(productos, filas), medicion);
    }
}
