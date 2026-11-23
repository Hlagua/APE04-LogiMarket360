namespace LogiMarket.Domain.Catalogo;

//patron composite (estructural)
//interfaz IElementoCatalogo
//rol: componente
//objetivo: ProductoSimple es la hoja y KitCompuesto el compuesto
//tambien es el "elemento" del patron Visitor y el agregado del patron Iterator
public interface IElementoCatalogo
{
    string Codigo { get; }
    string Nombre { get; }
    bool EsCompuesto { get; }

    //precio calculado recursivamente (hoja: precio propio
    //kit: Σ hijo x cantidad)
    decimal Precio { get; }

    //peso calculado recursivamente
    decimal PesoKg { get; }

    //este elemento es, o contiene directa o indirectamente, el codigo dado?
    bool Contiene(string codigo);

    void Aceptar(IVisitanteCatalogo visitante, int multiplicador = 1);

    IIteradorComponentes CrearIterador();
}
