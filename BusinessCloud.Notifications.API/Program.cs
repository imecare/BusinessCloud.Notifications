using Microsoft.Azure.Functions.Worker;
using Azure.Communication.Email;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(_ =>
{
    var connectionString = Environment.GetEnvironmentVariable("ACS_ConnectionString");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ACS_ConnectionString is not configured.");
    return new EmailClient(connectionString);
});

builder.Build().Run();
