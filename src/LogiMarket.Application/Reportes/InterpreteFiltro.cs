using System.Globalization;
using System.Text;
using LogiMarket.Application.Consultas;
using LogiMarket.Domain.Pedidos;

namespace LogiMarket.Application.Reportes;

/// <summary>
/// Patrón Interpreter para filtros de pedidos. Gramática (AND tiene mayor precedencia que OR):
/// <code>
///   expresion   := termino ( "OR" termino )*
///   termino     := factor ( "AND" factor )*
///   factor      := "(" expresion ")" | comparacion
///   comparacion := "ESTADO" ("=" | "!=") VALOR
///                | "TOTAL" ("=" | "!=" | "&gt;=" | "&lt;=" | "&gt;" | "&lt;") NUMERO
///                | "CLIENTE" ("=" | "!=") TEXTO        (TEXTO puede ir entre comillas simples)
/// </code>
/// El árbol se evalúa en memoria sobre DTO: nunca se concatena SQL.
/// </summary>
public interface IExpresionFiltro
{
    bool Interpretar(PedidoResumenDto pedido);
    string Describir();
}

public sealed class ExcepcionSintaxisFiltro : Exception
{
    public int Posicion { get; }

    public ExcepcionSintaxisFiltro(int posicion, string mensaje) : base($"Error de sintaxis en la posición {posicion + 1}: {mensaje}") =>
        Posicion = posicion;
}

// ---------------- Expresiones terminales ----------------

public sealed class ExpresionEstado : IExpresionFiltro
{
    private readonly bool _igual;
    private readonly EstadoPedido _estado;

    public ExpresionEstado(bool igual, EstadoPedido estado)
    {
        _igual = igual;
        _estado = estado;
    }

    public bool Interpretar(PedidoResumenDto p) => string.Equals(p.Estado, _estado.ToString(), StringComparison.OrdinalIgnoreCase) == _igual;
    public string Describir() => $"ESTADO {(_igual ? "=" : "!=")} {_estado}";
}

public sealed class ExpresionTotal : IExpresionFiltro
{
    private readonly string _operador;
    private readonly decimal _valor;

    public ExpresionTotal(string operador, decimal valor)
    {
        _operador = operador;
        _valor = valor;
    }

    public bool Interpretar(PedidoResumenDto p) => _operador switch
    {
        "=" => p.Total == _valor,
        "!=" => p.Total != _valor,
        ">=" => p.Total >= _valor,
        "<=" => p.Total <= _valor,
        ">" => p.Total > _valor,
        "<" => p.Total < _valor,
        _ => false
    };

    public string Describir() => $"TOTAL {_operador} {_valor.ToString("0.00", CultureInfo.InvariantCulture)}";
}

/// <summary>CLIENTE = texto: coincidencia parcial sin distinguir mayúsculas.</summary>
public sealed class ExpresionCliente : IExpresionFiltro
{
    private readonly bool _igual;
    private readonly string _texto;

    public ExpresionCliente(bool igual, string texto)
    {
        _igual = igual;
        _texto = texto;
    }

    public bool Interpretar(PedidoResumenDto p) => p.Cliente.Contains(_texto, StringComparison.OrdinalIgnoreCase) == _igual;
    public string Describir() => $"CLIENTE {(_igual ? "=" : "!=")} '{_texto}'";
}

// ---------------- Expresiones no terminales ----------------

public sealed class ExpresionY : IExpresionFiltro
{
    private readonly IExpresionFiltro _izq, _der;
    public ExpresionY(IExpresionFiltro izq, IExpresionFiltro der) { _izq = izq; _der = der; }
    public bool Interpretar(PedidoResumenDto p) => _izq.Interpretar(p) && _der.Interpretar(p);
    public string Describir() => $"({_izq.Describir()} AND {_der.Describir()})";
}

public sealed class ExpresionO : IExpresionFiltro
{
    private readonly IExpresionFiltro _izq, _der;
    public ExpresionO(IExpresionFiltro izq, IExpresionFiltro der) { _izq = izq; _der = der; }
    public bool Interpretar(PedidoResumenDto p) => _izq.Interpretar(p) || _der.Interpretar(p);
    public string Describir() => $"({_izq.Describir()} OR {_der.Describir()})";
}

// ---------------- Analizador léxico y sintáctico ----------------

internal enum TipoToken { Campo, Operador, Numero, Texto, Y, O, AbreParentesis, CierraParentesis, Fin }

internal sealed record Token(TipoToken Tipo, string Valor, int Posicion);

internal static class AnalizadorLexico
{
    private static readonly string[] Campos = { "ESTADO", "TOTAL", "CLIENTE" };

    public static List<Token> Tokenizar(string texto)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < texto.Length)
        {
            var c = texto[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '(') { tokens.Add(new Token(TipoToken.AbreParentesis, "(", i++)); continue; }
            if (c == ')') { tokens.Add(new Token(TipoToken.CierraParentesis, ")", i++)); continue; }
            if (c is '=' or '!' or '<' or '>')
            {
                var inicio = i;
                var op = c.ToString();
                if (i + 1 < texto.Length && texto[i + 1] == '=') { op += "="; i++; }
                i++;
                if (op == "!") throw new ExcepcionSintaxisFiltro(inicio, "operador '!' incompleto; use '!='.");
                tokens.Add(new Token(TipoToken.Operador, op, inicio));
                continue;
            }
            if (c == '\'')
            {
                var inicio = i++;
                var sb = new StringBuilder();
                while (i < texto.Length && texto[i] != '\'') sb.Append(texto[i++]);
                if (i >= texto.Length) throw new ExcepcionSintaxisFiltro(inicio, "falta cerrar la comilla simple.");
                i++;
                tokens.Add(new Token(TipoToken.Texto, sb.ToString(), inicio));
                continue;
            }
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            {
                var inicio = i;
                var sb = new StringBuilder();
                while (i < texto.Length && (char.IsLetterOrDigit(texto[i]) || texto[i] is '.' or '_' or '-')) sb.Append(texto[i++]);
                var palabra = sb.ToString();
                var mayus = palabra.ToUpperInvariant();
                if (mayus == "AND") tokens.Add(new Token(TipoToken.Y, mayus, inicio));
                else if (mayus == "OR") tokens.Add(new Token(TipoToken.O, mayus, inicio));
                else if (Campos.Contains(mayus) && EsCampoEsperado(tokens)) tokens.Add(new Token(TipoToken.Campo, mayus, inicio));
                else if (decimal.TryParse(palabra, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    tokens.Add(new Token(TipoToken.Numero, palabra, inicio));
                else tokens.Add(new Token(TipoToken.Texto, palabra, inicio));
                continue;
            }
            throw new ExcepcionSintaxisFiltro(i, $"carácter inesperado '{c}'.");
        }
        tokens.Add(new Token(TipoToken.Fin, "", texto.Length));
        return tokens;
    }

    // Un nombre de campo solo es campo al inicio o tras AND/OR/(; así "CLIENTE = Total" sigue siendo texto.
    private static bool EsCampoEsperado(List<Token> previos) =>
        previos.Count == 0 || previos[^1].Tipo is TipoToken.Y or TipoToken.O or TipoToken.AbreParentesis;
}

/// <summary>Analizador sintáctico descendente recursivo que construye el árbol de expresiones.</summary>
public static class AnalizadorFiltro
{
    public static IExpresionFiltro Analizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new ExcepcionSintaxisFiltro(0, "el filtro está vacío.");
        var parser = new Parser(AnalizadorLexico.Tokenizar(texto));
        var expresion = parser.Expresion();
        parser.Esperar(TipoToken.Fin, "fin del filtro");
        return expresion;
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _i;

        public Parser(List<Token> tokens) => _tokens = tokens;

        private Token Actual => _tokens[_i];

        public Token Esperar(TipoToken tipo, string descripcion)
        {
            if (Actual.Tipo != tipo)
                throw new ExcepcionSintaxisFiltro(Actual.Posicion,
                    $"se esperaba {descripcion} y se encontró '{(Actual.Tipo == TipoToken.Fin ? "fin" : Actual.Valor)}'.");
            return _tokens[_i++];
        }

        public IExpresionFiltro Expresion()
        {
            var izquierda = Termino();
            while (Actual.Tipo == TipoToken.O)
            {
                _i++;
                izquierda = new ExpresionO(izquierda, Termino());
            }
            return izquierda;
        }

        private IExpresionFiltro Termino()
        {
            var izquierda = Factor();
            while (Actual.Tipo == TipoToken.Y)
            {
                _i++;
                izquierda = new ExpresionY(izquierda, Factor());
            }
            return izquierda;
        }

        private IExpresionFiltro Factor()
        {
            if (Actual.Tipo == TipoToken.AbreParentesis)
            {
                _i++;
                var interna = Expresion();
                Esperar(TipoToken.CierraParentesis, "')'");
                return interna;
            }
            return Comparacion();
        }

        private IExpresionFiltro Comparacion()
        {
            var campo = Esperar(TipoToken.Campo, "un campo (ESTADO, TOTAL o CLIENTE)");
            var operador = Esperar(TipoToken.Operador, "un operador");
            switch (campo.Valor)
            {
                case "TOTAL":
                {
                    var numero = Esperar(TipoToken.Numero, "un número");
                    return new ExpresionTotal(operador.Valor, decimal.Parse(numero.Valor, CultureInfo.InvariantCulture));
                }
                case "ESTADO":
                {
                    ExigirIgualdad(operador, campo.Valor);
                    var valor = Valor();
                    if (!Enum.TryParse<EstadoPedido>(valor.Valor, true, out var estado))
                        throw new ExcepcionSintaxisFiltro(valor.Posicion,
                            $"estado desconocido '{valor.Valor}'. Valores: {string.Join(", ", Enum.GetNames<EstadoPedido>())}.");
                    return new ExpresionEstado(operador.Valor == "=", estado);
                }
                default:
                {
                    ExigirIgualdad(operador, campo.Valor);
                    var valor = Valor();
                    return new ExpresionCliente(operador.Valor == "=", valor.Valor);
                }
            }
        }

        private Token Valor()
        {
            if (Actual.Tipo is TipoToken.Texto or TipoToken.Numero) return _tokens[_i++];
            throw new ExcepcionSintaxisFiltro(Actual.Posicion, "se esperaba un valor.");
        }

        private static void ExigirIgualdad(Token operador, string campo)
        {
            if (operador.Valor is not ("=" or "!="))
                throw new ExcepcionSintaxisFiltro(operador.Posicion, $"el campo {campo} solo admite '=' o '!='.");
        }
    }
}
