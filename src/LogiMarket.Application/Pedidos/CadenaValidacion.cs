using LogiMarket.Domain.Comun;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Clientes;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Pedidos.Estados;
using LogiMarket.Application.Contratos;

namespace LogiMarket.Application.Pedidos;

public sealed record RechazoValidacion(string Eslabon, string Codigo, string Mensaje);

public sealed record ResultadoValidacion(bool EsValido, RechazoValidacion? Rechazo, IReadOnlyList<string> Recorrido)
{
    public override string ToString() =>
        EsValido ? $"VÁLIDO ({string.Join(" → ", Recorrido)})" : $"RECHAZO en {Rechazo!.Eslabon}: {Rechazo.Mensaje}";
}

//datos que recorren la cadena
//el stock disponible se lee dentro de la transaccion
public sealed class ContextoValidacion
{
    public required Pedido Pedido { get; init; }
    public Cliente? Cliente { get; init; }
    public required IReadOnlyDictionary<string, int> StockDisponible { get; init; }
    public UsuarioSesion? Usuario { get; init; }
}

//patron chain of responsibility (comportamental)
//clase EslabonValidacion
//objetivo: cada eslabon evalua una condicion
//si falla devuelve un rechazo estructurado y detiene la cadena, si no delega en el siguiente
//no sustituye a las invariantes del dominio
public abstract class EslabonValidacion
{
    private EslabonValidacion? _siguiente;

    public abstract string Nombre { get; }

    public EslabonValidacion Enlazar(EslabonValidacion siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public ResultadoValidacion Validar(ContextoValidacion contexto) => Validar(contexto, new List<string>());

    private ResultadoValidacion Validar(ContextoValidacion contexto, List<string> recorrido)
    {
        recorrido.Add(Nombre);
        var rechazo = Evaluar(contexto);
        if (rechazo is not null) return new ResultadoValidacion(false, rechazo, recorrido);
        return _siguiente is null
            ? new ResultadoValidacion(true, null, recorrido)
            : _siguiente.Validar(contexto, recorrido);
    }

    protected abstract RechazoValidacion? Evaluar(ContextoValidacion contexto);

    protected RechazoValidacion Rechazar(string codigo, string mensaje) => new(Nombre, codigo, mensaje);
}

public sealed class ClienteActivoValidador : EslabonValidacion
{
    public override string Nombre => "ClienteActivo";

    protected override RechazoValidacion? Evaluar(ContextoValidacion c)
    {
        if (c.Cliente is null) return Rechazar("CLIENTE_REQUERIDO", "El pedido no tiene un cliente registrado.");
        if (!c.Cliente.Activo) return Rechazar("CLIENTE_INACTIVO", $"El cliente {c.Cliente.Nombre} está inactivo.");
        return null;
    }
}

public sealed class LineasCantidadesValidador : EslabonValidacion
{
    public const int MaximoPorLinea = ParametrosNegocio.CantidadMaximaPorLinea;
    public override string Nombre => "LineasCantidades";

    protected override RechazoValidacion? Evaluar(ContextoValidacion c)
    {
        if (c.Pedido.Lineas.Count == 0) return Rechazar("PEDIDO_SIN_LINEAS", "El pedido no tiene líneas.");
        var invalida = c.Pedido.Lineas.FirstOrDefault(l => l.Cantidad <= 0 || l.Cantidad > MaximoPorLinea);
        return invalida is null
            ? null
            : Rechazar("CANTIDAD_INVALIDA", $"Cantidad inválida en {invalida.Codigo}: {invalida.Cantidad}.");
    }
}

public sealed class StockDisponibleValidador : EslabonValidacion
{
    public override string Nombre => "StockDisponible";

    protected override RechazoValidacion? Evaluar(ContextoValidacion c)
    {
        foreach (var (sku, requerido) in c.Pedido.RequerimientoPorSku())
        {
            var disponible = c.StockDisponible.GetValueOrDefault(sku);
            if (requerido > disponible)
                return Rechazar("STOCK_INSUFICIENTE",
                    $"{sku} insuficiente: se requieren {requerido} y solo existen {disponible}.");
        }
        return null;
    }
}

public sealed class CondicionesConfirmacionValidador : EslabonValidacion
{
    private readonly IPoliticaPermisos _permisos;

    public CondicionesConfirmacionValidador(IPoliticaPermisos permisos) => _permisos = permisos;

    public override string Nombre => "AutorizacionCondiciones";

    protected override RechazoValidacion? Evaluar(ContextoValidacion c)
    {
        if (!_permisos.Permite(c.Usuario?.Rol, Operacion.ConfirmarPedido))
            return Rechazar("NO_AUTORIZADO", $"El rol {c.Usuario?.Rol.ToString() ?? "anónimo"} no puede confirmar pedidos.");
        var estado = EstadosPedido.De(c.Pedido.Estado);
        if (c.Pedido.Estado != EstadoPedido.BORRADOR && !estado.PermiteCobro)
            return Rechazar("ESTADO_INVALIDO", $"El pedido está en estado {c.Pedido.Estado} y no admite confirmación.");
        if (c.Pedido.SubtotalCalculado <= 0) return Rechazar("TOTAL_INVALIDO", "El total del pedido debe ser positivo.");
        return null;
    }
}

//arma la cadena en el orden exigido: cliente -> lineas -> stock -> autorizacion/condiciones
public sealed class CadenaValidacionPedido : ICadenaValidacionPedido
{
    private readonly EslabonValidacion _cabeza;

    public CadenaValidacionPedido(IPoliticaPermisos permisos)
    {
        _cabeza = new ClienteActivoValidador();
        _cabeza.Enlazar(new LineasCantidadesValidador())
               .Enlazar(new StockDisponibleValidador())
               .Enlazar(new CondicionesConfirmacionValidador(permisos));
    }

    public ResultadoValidacion Validar(ContextoValidacion contexto) => _cabeza.Validar(contexto);
}
