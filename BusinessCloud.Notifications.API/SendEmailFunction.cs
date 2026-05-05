using Microsoft.AspNetCore.Http;
using System.Net;
using Azure;
using Azure.Communication.Email;
using BusinessCloud.Notifications.API.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace BusinessCloud.Notifications.API;

public class SendEmailFunction
{
    private readonly ILogger<SendEmailFunction> _logger;
    private readonly EmailClient _emailClient;

    public SendEmailFunction(ILogger<SendEmailFunction> logger, EmailClient emailClient)
    {
        _logger = logger;
        _emailClient = emailClient;
    }

    [Function("SendEmail")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "options")] HttpRequestData req)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            return CreateCorsResponse(req, HttpStatusCode.NoContent);

        _logger.LogInformation("SendEmail function invoked.");

        var senderAddress = Environment.GetEnvironmentVariable("EMAIL_SENDER_ADDRESS");
        var acsConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING"));

        if (!acsConfigured || string.IsNullOrWhiteSpace(senderAddress))
        {
            _logger.LogError("Missing required configuration: ACS_CONNECTION_STRING or EMAIL_SENDER_ADDRESS.");
            return await CreateJsonResponse(req, HttpStatusCode.InternalServerError, new
            {
                ok = false,
                code = "MissingConfiguration",
                message = "El servidor no tiene la configuración de correo completa. Contacte al administrador."
            });
        }

        SendEmailRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<SendEmailRequest>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize request body.");
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new
            {
                ok = false,
                code = "InvalidPayload",
                message = "El cuerpo del request no es un JSON válido."
            });
        }

        if (payload is null
            || payload.To is null || payload.To.Length == 0
            || string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Body))
        {
            _logger.LogWarning("Payload validation failed.");
            return await CreateJsonResponse(req, HttpStatusCode.BadRequest, new
            {
                ok = false,
                code = "InvalidPayload",
                message = "Los campos 'to' (al menos 1), 'subject' y 'body' son requeridos."
            });
        }

        try
        {
            _logger.LogInformation(
                "Sending email for system '{SystemId}' to {RecipientCount} recipient(s). Subject: '{Subject}'",
                payload.SystemId ?? "N/A", payload.To.Length, payload.Subject);

            var content = new EmailContent(payload.Subject) { Html = payload.Body };
            var recipients = new EmailRecipients(payload.To.Select(t => new EmailAddress(t)).ToList());
            var message = new EmailMessage(senderAddress, recipients, content);

            EmailSendOperation operation = await _emailClient.SendAsync(WaitUntil.Completed, message);

            _logger.LogInformation(
                "Email sent successfully. OperationId: {OperationId}, Status: {Status}",
                operation.Id, operation.Value.Status);

            return await CreateJsonResponse(req, HttpStatusCode.OK, new
            {
                ok = true,
                status = operation.Value.Status.ToString(),
                messageId = operation.Id,
                systemId = payload.SystemId
            });
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "ACS email send failed. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}",
                ex.Status, ex.ErrorCode);

            return await CreateJsonResponse(req, HttpStatusCode.BadGateway, new
            {
                ok = false,
                code = ex.ErrorCode ?? "EmailSendFailed",
                message = "Error al enviar el correo a través de Azure Communication Services.",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email for system '{SystemId}'.", payload.SystemId);

            return await CreateJsonResponse(req, HttpStatusCode.InternalServerError, new
            {
                ok = false,
                code = "InternalError",
                message = "Error interno del servidor."
            });
        }
    }

    private static HttpResponseData CreateCorsResponse(HttpRequestData req, HttpStatusCode statusCode)
    {
        var response = req.CreateResponse(statusCode);
        AddCorsHeaders(response, req);
        return response;
    }

    private static async Task<HttpResponseData> CreateJsonResponse<T>(HttpRequestData req, HttpStatusCode statusCode, T body)
    {
        var response = req.CreateResponse(statusCode);
        AddCorsHeaders(response, req);
        await response.WriteAsJsonAsync(body);
        return response;
    }

    private static void AddCorsHeaders(HttpResponseData response, HttpRequestData req)
    {
        var allowedOrigin = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGIN") ?? "*";
        response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, x-functions-key");
    }
}