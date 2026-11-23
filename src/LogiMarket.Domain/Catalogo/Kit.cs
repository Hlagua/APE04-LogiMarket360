using LogiMarket.Domain.Comun;

namespace LogiMarket.Domain.Catalogo;

//kit persistido
//cada componente referencia con FK real a un Producto o a otro Kit (exactamente uno)
//la deteccion de ciclos indirectos se hace con ArbolCatalogo antes de persistir
public class Kit
{
    private readonly List<KitComponente> _componentes = new();

    public int Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; }
    public IReadOnlyCollection<KitComponente> Componentes => _componentes.AsReadOnly();

    private Kit() { }

    public static Kit Crear(string codigo, string nombre)
    {
        return new Kit
        {
            Codigo = Producto.NormalizarSku(codigo),
            Nombre = Guardia.Texto(nombre, "nombre", 120),
            Activo = true
        };
    }

    public void Renombrar(string nombre) => Nombre = Guardia.Texto(nombre, "nombre", 120);

    public void AgregarComponente(Producto producto, int cantidad) =>
        Agregar(producto.Sku, producto.Id, null, cantidad);

    public void AgregarComponente(Kit kitHijo, int cantidad)
    {
        if (kitHijo.Codigo == Codigo || (kitHijo.Id != 0 && kitHijo.Id == Id))
            throw new ExcepcionDominio("KIT_CICLO", $"El kit {Codigo} no puede contenerse a sí mismo.");
        Agregar(kitHijo.Codigo, null, kitHijo.Id, cantidad);
    }

    private void Agregar(string codigo, int? productoId, int? kitHijoId, int cantidad)
    {
        Guardia.CantidadPositiva(cantidad);
        var existente = _componentes.FirstOrDefault(c => c.CodigoComponente == codigo);
        if (existente is not null) existente.CambiarCantidad(existente.Cantidad + cantidad);
        else _componentes.Add(new KitComponente(codigo, productoId, kitHijoId, cantidad));
    }

    public void CambiarCantidadComponente(string codigoComponente, int cantidad)
    {
        var codigo = Producto.NormalizarSku(codigoComponente);
        var c = _componentes.FirstOrDefault(x => x.CodigoComponente == codigo)
                ?? throw new ExcepcionDominio("COMPONENTE_NO_EXISTE", $"El kit {Codigo} no contiene {codigo}.");
        c.CambiarCantidad(cantidad);
    }

    public void QuitarComponente(string codigoComponente)
    {
        var codigo = Producto.NormalizarSku(codigoComponente);
        if (_componentes.RemoveAll(c => c.CodigoComponente == codigo) == 0)
            throw new ExcepcionDominio("COMPONENTE_NO_EXISTE", $"El kit {Codigo} no contiene {codigo}.");
    }
}

public class KitComponente
{
    public int Id { get; private set; }
    public int KitId { get; private set; }
    public string CodigoComponente { get; private set; } = string.Empty;
    public int? ProductoId { get; private set; }
    public int? KitHijoId { get; private set; }
    public int Cantidad { get; private set; }
    public bool EsKit => KitHijoId is not null;

    private KitComponente() { }

    internal KitComponente(string codigo, int? productoId, int? kitHijoId, int cantidad)
    {
        if ((productoId is null) == (kitHijoId is null))
            throw new ExcepcionDominio("COMPONENTE_INVALIDO", "Un componente referencia exactamente un producto o un kit.");
        CodigoComponente = codigo;
        ProductoId = productoId;
        KitHijoId = kitHijoId;
        Cantidad = Guardia.CantidadPositiva(cantidad);
    }

    internal void CambiarCantidad(int cantidad) => Cantidad = Guardia.CantidadPositiva(cantidad);
}
