using System.Text.Json.Serialization;

namespace BusinessCloud.Notifications.API.Models;

public class SendEmailRequest
{
    [JsonPropertyName("to")]
    public string[]? To { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("systemId")]
    public string? SystemId { get; set; }
}
