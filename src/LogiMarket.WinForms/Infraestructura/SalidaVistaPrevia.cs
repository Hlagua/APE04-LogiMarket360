using LogiMarket.Application.Puertos;

namespace LogiMarket.WinForms.Infraestructura;

/// <summary>Implementador del Bridge en la presentación: muestra el comprobante en una ventana visible.</summary>
public sealed class SalidaVistaPrevia : ISalidaDocumento
{
    public const string NombreMedio = "VistaPrevia";
    public string Nombre => NombreMedio;

    public Task<ResultadoSalida> EmitirAsync(string titulo, string contenido, CancellationToken ct = default)
    {
        using var ventana = new Form
        {
            Text = $"Vista previa - {titulo}",
            Size = new Size(640, 560),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            KeyPreview = true
        };
        var texto = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10f), Text = contenido.Replace("\n", Environment.NewLine).Replace("\r\r", "\r"), WordWrap = false
        };
        var cerrar = new Button { Text = "Cerrar", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.OK };
        ventana.Controls.Add(texto);
        ventana.Controls.Add(cerrar);
        ventana.AcceptButton = cerrar;
        ventana.CancelButton = cerrar;
        ventana.ShowDialog();
        return Task.FromResult(new ResultadoSalida(true, Nombre, "Ventana de vista previa", contenido.Length));
    }
}
