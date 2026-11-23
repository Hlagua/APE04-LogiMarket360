using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Clientes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Pedidos;
using LogiMarket.WinForms.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Contratos;

namespace LogiMarket.WinForms.Vistas;

/// <summary>05. Carrito y borrador: Builder al crear, Memento para deshacer/rehacer y valoración de Application.</summary>
public sealed class VistaCarrito : VistaBase
{
    private readonly ComboBox _cliente = Ui.Combo(220), _entrega = Ui.Combo(110), _elemento = Ui.Combo(300);
    private readonly NumericUpDown _cantidad = Ui.Numero(1, 1000, 0, 1);
    private readonly DataGridView _lineas = Ui.Grilla();
    private readonly Label _totales = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f), Margin = new Padding(3, 8, 3, 8) };
    private readonly Label _estado = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _deshacer, _rehacer;
    private readonly List<Control> _edicion = new();
    private EditorBorrador? _editor;
    private bool _cargando;

    public VistaCarrito(Ejecutor ejecutor, IMediadorVistas mediador) : base(ejecutor, mediador, FormularioPrincipalMediator.Carrito)
    {
        _entrega.DataSource = Enum.GetValues<TipoEntrega>();
        _cliente.SelectedIndexChanged += async (_, _) => await CambiarClienteAsync();
        _entrega.SelectedIndexChanged += (_, _) => Aplicar(e => e.CambiarEntrega((TipoEntrega)_entrega.SelectedItem!));
        _deshacer = Ui.Boton("Deshacer (Ctrl+Z)", (_, _) => Aplicar(e => e.Deshacer()));
        _rehacer = Ui.Boton("Rehacer (Ctrl+Y)", (_, _) => Aplicar(e => e.Rehacer()));

        var seleccion = Ui.Barra(Ui.Etiqueta("Cliente"), Ed(_cliente), Ui.Etiqueta("Entrega"), Ed(_entrega));
        var agregar = Ui.Barra(Ui.Etiqueta("Producto / kit"), Ed(_elemento), Ui.Etiqueta("Cantidad"), Ed(_cantidad),
            Ed(Ui.Boton("Agregar", async (_, _) => await AgregarAsync(), true)),
            Ed(Ui.Boton("Cambiar cantidad", (_, _) => CambiarCantidad())),
            Ed(Ui.Boton("Quitar línea", (_, _) => Quitar())),
            Ed(Ui.Boton("Vaciar carrito", (_, _) => { if (Ui.Confirmar("¿Vaciar el carrito? (puede deshacerlo)")) Aplicar(e => e.Vaciar()); })),
            Ed(_deshacer), Ed(_rehacer));
        var acciones = Ui.Barra(
            Ed(Ui.Boton("Nuevo carrito", (_, _) => Nuevo())),
            Ed(Ui.Boton("Guardar borrador", async (_, _) => await GuardarAsync())),
            Ed(Ui.Boton("Guardar y validar", async (_, _) => await ValidarAsync())),
            Ed(Ui.Boton("Guardar e ir a cobro", async (_, _) => await IrACobroAsync(), true)), _estado);
        Ui.Ajustable(_totales, this);
        Controls.Add(Ui.Layout("Carrito / borrador", seleccion, agregar, _lineas, _totales, acciones));

        // Atajos de teclado para deshacer/rehacer.
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Z) { Aplicar(ed => ed.Deshacer()); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.Y) { Aplicar(ed => ed.Rehacer()); e.Handled = true; }
        };
        _lineas.KeyDown += (_, e) => OnKeyDown(e);
    }

    private Control Ed(Control c)
    {
        _edicion.Add(c);
        return c;
    }

    public bool TieneCambiosSinGuardar => _editor?.TieneCambiosSinGuardar == true;

    public override async Task RefrescarAsync()
    {
        _cargando = true;
        var seleccionCliente = _cliente.SelectedValue;
        var seleccionElemento = _elemento.SelectedValue;
        var (ok, clientes) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().ClientesAsync(null));
        if (ok && clientes is not null)
        {
            _cliente.DisplayMember = nameof(ClienteDto.Nombre);
            _cliente.ValueMember = nameof(ClienteDto.Id);
            _cliente.DataSource = clientes.Where(c => c.Activo).Select(c => c with { Nombre = $"{c.Nombre} ({c.Categoria})" }).ToList();
            if (seleccionCliente is not null) _cliente.SelectedValue = seleccionCliente;
        }
        var (okE, elementos) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().VendiblesAsync());
        if (okE && elementos is not null)
        {
            _elemento.DisplayMember = "Texto";
            _elemento.ValueMember = "Codigo";
            _elemento.DataSource = elementos.Select(e => new { e.Codigo, Texto = $"{e} - disponible {e.StockDisponible}" }).ToList();
            if (seleccionElemento is not null) _elemento.SelectedValue = seleccionElemento;
        }
        _cargando = false;
        _edicion.ForEach(c => c.Enabled = Puede(Operacion.GestionarBorradores));
        Mostrar();
    }

    /// <summary>Abre un borrador existente (enviado por el Mediator desde Pedidos).</summary>
    public override async Task RecibirPedidoAsync(int pedidoId)
    {
        if (!ConfirmarDescarte()) return;
        var (ok, editor) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioPedidos>().AbrirEditorAsync(pedidoId));
        if (!ok || editor is null) return;
        _editor = editor;
        _cargando = true;
        _cliente.SelectedValue = editor.Pedido!.ClienteId;
        _entrega.SelectedItem = editor.Pedido.TipoEntrega;
        _cargando = false;
        Mostrar();
    }

    private bool ConfirmarDescarte() =>
        !TieneCambiosSinGuardar || Ui.Confirmar("El carrito tiene cambios sin guardar. ¿Descartarlos?", "Cambios sin guardar");

    private void Nuevo()
    {
        if (!ConfirmarDescarte()) return;
        _editor = null;
        Mostrar();
    }

    private EditorBorrador Editor()
    {
        if (_editor is null)
        {
            // El editor solo conserva colaboradores singleton (valoración), por eso el ámbito puede cerrarse.
            using var scope = Ejecutor.Singleton<IServiceProvider>().CreateScope();
            _editor = scope.ServiceProvider.GetRequiredService<IServicioPedidos>().NuevoEditor();
            _editor.CambiarEntrega((TipoEntrega)_entrega.SelectedItem!);
        }
        return _editor;
    }

    private async Task CambiarClienteAsync()
    {
        if (_cargando || _cliente.SelectedValue is not int id) return;
        var (ok, cliente) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().ClienteAsync(id));
        if (ok && cliente is not null && Editor().Pedido?.ClienteId != id) Aplicar(e => e.SeleccionarCliente(cliente));
    }

    private async Task AgregarAsync()
    {
        if (!Validar(_cliente, _cliente.SelectedValue is int, "Seleccione un cliente") ||
            !Validar(_elemento, _elemento.SelectedValue is string, "Seleccione un producto o kit")) return;
        if (Editor().Pedido is null) await CambiarClienteAsync();
        var codigo = (string)_elemento.SelectedValue!;
        var (ok, elemento) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioConsultas>().ElementoAsync(codigo));
        if (ok && elemento is not null) Aplicar(e => e.Agregar(elemento, (int)_cantidad.Value));
    }

    private string? LineaSeleccionada() => _lineas.CurrentRow?.Cells["Codigo"].Value as string;

    private void CambiarCantidad()
    {
        if (LineaSeleccionada() is { } codigo) Aplicar(e => e.CambiarCantidad(codigo, (int)_cantidad.Value));
    }

    private void Quitar()
    {
        if (LineaSeleccionada() is { } codigo && Ui.Confirmar($"¿Quitar la línea {codigo}? (puede deshacerlo)")) Aplicar(e => e.Quitar(codigo));
    }

    /// <summary>Aplica un cambio sobre el editor mostrando reglas violadas como mensajes claros.</summary>
    private void Aplicar(Action<EditorBorrador> cambio)
    {
        if (_cargando) return;
        try
        {
            cambio(Editor());
        }
        catch (Exception ex) when (ex is Domain.Comun.ExcepcionDominio or Application.Comun.ExcepcionAplicacion or Application.Comun.AccesoDenegadoException)
        {
            MessageBox.Show(ex.Message, "No se pudo modificar el borrador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Mostrar();
    }

    private void Mostrar()
    {
        var pedido = _editor?.Pedido;
        Ui.Enlazar(_lineas, pedido?.Lineas.Select(l => new
        {
            l.Codigo, l.Descripcion, Tipo = l.EsKit ? "Kit" : "Producto", l.Cantidad, PrecioUnitario = l.PrecioUnitario, Subtotal = l.Subtotal,
            Composicion = string.Join(" + ", l.Componentes.Select(c => $"{c.Sku}x{c.CantidadPorUnidad}"))
        }) ?? Enumerable.Empty<object>().Select(_ => new { Codigo = "", Descripcion = "", Tipo = "", Cantidad = 0, PrecioUnitario = 0m, Subtotal = 0m, Composicion = "" }), "Codigo");

        var v = _editor?.Valorar();
        _totales.Text = v is null
            ? "Carrito vacío: seleccione cliente y agregue productos o kits."
            : $"Subtotal {Ui.Usd(v.Subtotal)}   −  Descuento {v.Politica} ({v.Tasa:P0}) {Ui.Usd(v.Descuento)}   +  Envío {v.Familia} {Ui.Usd(v.Tarifa)} ({v.PlazoDias} días)   =   TOTAL {Ui.Usd(v.Total)}   |  {v.Unidades} unidades físicas";
        _deshacer.Enabled = _editor?.Historial.PuedeDeshacer == true;
        _rehacer.Enabled = _editor?.Historial.PuedeRehacer == true;
        _estado.Text = _editor is null ? "" :
            $"{(_editor.PedidoId == 0 ? "Borrador nuevo" : $"Borrador PED-{_editor.PedidoId:000000}")}" +
            $"{(_editor.TieneCambiosSinGuardar ? " • cambios sin guardar" : " • guardado")} • deshacer: {_editor.Historial.CantidadDeshacer}/5";
    }

    private async Task<int> GuardarAsync()
    {
        if (_editor?.Pedido is null)
        {
            MessageBox.Show("El carrito está vacío.", "Guardar borrador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        var (_, r) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioPedidos>().GuardarBorradorAsync(_editor));
        if (!Ejecutor.Mostrar(r)) return 0;
        Mostrar();
        await NotificarAsync(EventoVista.BorradorGuardado, r!.Valor);
        return r.Valor;
    }

    private async Task ValidarAsync()
    {
        var id = await GuardarAsync();
        if (id == 0) return;
        var (ok, v) = await Ejecutor.Ejecutar(sp => sp.GetRequiredService<IServicioPedidos>().ValidarAsync(id));
        if (!ok || v is null) return;
        if (!v.EsValido)
        {
            MessageBox.Show($"{v.Rechazo!.Mensaje}\n\nEslabones evaluados: {string.Join(" → ", v.Recorrido)}",
                $"Validación rechazada en {v.Rechazo.Eslabon}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Ejecutor.Estado($"PED-{id:000000} VALIDADO ({string.Join(" → ", v.Recorrido)}).");
        _editor = null; // un pedido validado ya no es editable
        Mostrar();
        await NotificarAsync(EventoVista.PedidoCambiado, id);
    }

    private async Task IrACobroAsync()
    {
        var id = await GuardarAsync();
        if (id == 0) return;
        _editor!.MarcarGuardado();
        _editor = null;
        Mostrar();
        await NotificarAsync(EventoVista.AbrirPedidoEnCobro, id);
    }
}
