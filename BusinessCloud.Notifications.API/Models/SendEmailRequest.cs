using System.ComponentModel.DataAnnotations;

namespace BusinessCloud.Notifications.API.Models;

public class SendEmailRequest
{
    [Required(ErrorMessage = "La lista de destinatarios es obligatoria.")]
    [MinLength(1, ErrorMessage = "Debe incluir al menos un destinatario.")]
    public List<string> To { get; set; } = [];

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [StringLength(200, ErrorMessage = "El asunto no puede exceder 200 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "El cuerpo del correo es obligatorio.")]
    public string Body { get; set; } = string.Empty;

    [Required(ErrorMessage = "El SystemId es obligatorio.")]
    [RegularExpression("^(Transportes|Abonos|Bazar)$", ErrorMessage = "SystemId debe ser 'Transportes', 'Abonos' o 'Bazar'.")]
    public string SystemId { get; set; } = string.Empty;
}
