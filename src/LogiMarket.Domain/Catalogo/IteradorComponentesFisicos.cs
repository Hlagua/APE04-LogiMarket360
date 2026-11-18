namespace LogiMarket.Domain.Catalogo;

/// <summary>Componente físico final (SKU) con la cantidad acumulada propagada desde los kits.</summary>
public sealed record ComponenteFisico(string Sku, string Nombre, int Cantidad, decimal PrecioUnitario, decimal PesoKg);

/// <summary>Patrón Iterator: recorrido externo de los componentes físicos de un elemento.</summary>
public interface IIteradorComponentes
{
    bool MoverSiguiente();
    ComponenteFisico Actual { get; }
    void Reiniciar();
}

/// <summary>
/// Iterador propio en profundidad con pila explícita: determinista (orden de inserción), propaga
/// cantidades multiplicándolas por nivel y termina siempre (los ciclos se rechazan al construir el kit
/// y además se limita la profundidad como defensa).
/// </summary>
public sealed class IteradorComponentesFisicos : IIteradorComponentes
{
    private const int ProfundidadMaxima = 32;
    private readonly IElementoCatalogo _raiz;
    private readonly Stack<(IElementoCatalogo Elemento, int Multiplicador, int Profundidad)> _pila = new();
    private ComponenteFisico? _actual;

    public IteradorComponentesFisicos(IElementoCatalogo raiz)
    {
        _raiz = raiz;
        Reiniciar();
    }

    public ComponenteFisico Actual =>
        _actual ?? throw new InvalidOperationException("Llame a MoverSiguiente antes de leer Actual.");

    public void Reiniciar()
    {
        _pila.Clear();
        _actual = null;
        _pila.Push((_raiz, 1, 0));
    }

    public bool MoverSiguiente()
    {
        while (_pila.Count > 0)
        {
            var (elemento, multiplicador, profundidad) = _pila.Pop();
            if (profundidad > ProfundidadMaxima)
                throw new InvalidOperationException("Profundidad de kit excedida: posible ciclo.");

            if (elemento is KitCompuesto kit)
            {
                // Se apilan en orden inverso para respetar el orden de inserción al desapilar.
                var hijos = kit.Hijos;
                for (var i = hijos.Count - 1; i >= 0; i--)
                    _pila.Push((hijos[i].Elemento, multiplicador * hijos[i].Cantidad, profundidad + 1));
                continue;
            }

            _actual = new ComponenteFisico(elemento.Codigo, elemento.Nombre, multiplicador, elemento.Precio, elemento.PesoKg);
            return true;
        }
        _actual = null;
        return false;
    }
}
