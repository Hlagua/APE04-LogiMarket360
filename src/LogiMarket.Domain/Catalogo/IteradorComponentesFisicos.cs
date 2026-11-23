namespace LogiMarket.Domain.Catalogo;

//componente fisico final (SKU) con la cantidad acumulada propagada desde los kits
public sealed record ComponenteFisico(string Sku, string Nombre, int Cantidad, decimal PrecioUnitario, decimal PesoKg);

//patron iterator (comportamental)
//interfaz IIteradorComponentes
//objetivo: recorrido externo de los componentes fisicos de un elemento
public interface IIteradorComponentes
{
    bool MoverSiguiente();
    ComponenteFisico Actual { get; }
    void Reiniciar();
}

//iterador propio en profundidad con pila explicita: determinista (orden de insercion), propaga
//cantidades multiplicandolas por nivel y termina siempre (los ciclos se rechazan al construir el kit
//y ademas se limita la profundidad como defensa)
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
                //se apilan en orden inverso para respetar el orden de insercion al desapilar
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
