using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
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
    private readonly string _senderAddress;

    public SendEmailFunction(ILogger<SendEmailFunction> logger, EmailClient emailClient)
    {
        _logger = logger;
        _emailClient = emailClient;

        _senderAddress = Environment.GetEnvironmentVariable("ACS_SenderAddress")
            ?? throw new InvalidOperationException("La variable de entorno 'ACS_SenderAddress' no está configurada.");
    }

    [Function("SendEmail")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("SendEmail function invoked.");

        SendEmailRequest? emailRequest;
        try
        {
            emailRequest = await req.ReadFromJsonAsync<SendEmailRequest>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al deserializar el request body.");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { Error = "El cuerpo del request no es un JSON válido." });
            return badResponse;
        }

        if (emailRequest is null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { Error = "El cuerpo del request es requerido." });
            return badResponse;
        }

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(emailRequest, new ValidationContext(emailRequest), validationResults, true))
        {
            var errors = validationResults.Select(v => v.ErrorMessage).ToList();
            _logger.LogWarning("Validación fallida: {Errors}", string.Join("; ", errors));
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { Errors = errors });
            return badResponse;
        }

        try
        {
            _logger.LogInformation(
                "Enviando correo desde sistema '{SystemId}' a {RecipientCount} destinatario(s). Asunto: '{Subject}'",
                emailRequest.SystemId, emailRequest.To.Count, emailRequest.Subject);

            var emailContent = new EmailContent(emailRequest.Subject)
            {
                Html = emailRequest.Body
            };

            var recipients = new EmailRecipients(
                emailRequest.To.Select(to => new EmailAddress(to)).ToList());

            var emailMessage = new EmailMessage(_senderAddress, recipients, emailContent);

            EmailSendOperation operation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);

            _logger.LogInformation(
                "Correo enviado exitosamente. OperationId: {OperationId}, Status: {Status}",
                operation.Id, operation.Value.Status);

            var okResponse = req.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(new
            {
                Message = "Correo enviado exitosamente.",
                OperationId = operation.Id,
                Status = operation.Value.Status.ToString()
            });
            return okResponse;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Error de Azure Communication Services al enviar correo. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}",
                ex.Status, ex.ErrorCode);

            var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse.WriteAsJsonAsync(new
            {
                Error = "Error al enviar el correo a través de Azure Communication Services.",
                Detail = ex.Message
            });
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al enviar correo para sistema '{SystemId}'.", emailRequest.SystemId);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { Error = "Error interno del servidor." });
            return errorResponse;
        }
    }
}