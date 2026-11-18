namespace LogiMarket.Domain.Catalogo;

/// <summary>
/// Patrón Composite (componente). ProductoSimple es la hoja y KitCompuesto el compuesto.
/// También es el "elemento" del patrón Visitor y el agregado del patrón Iterator.
/// </summary>
public interface IElementoCatalogo
{
    string Codigo { get; }
    string Nombre { get; }
    bool EsCompuesto { get; }

    /// <summary>Precio calculado recursivamente (hoja: precio propio; kit: Σ hijo × cantidad).</summary>
    decimal Precio { get; }

    /// <summary>Peso calculado recursivamente.</summary>
    decimal PesoKg { get; }

    /// <summary>¿Este elemento es, o contiene directa o indirectamente, el código dado?</summary>
    bool Contiene(string codigo);

    void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1);

    IIteradorComponentes CrearIterador();
}
