using FleetPulse.Api;
using FleetPulse.Api.Contracts;
using FleetPulse.Application;
using Microsoft.AspNetCore.Diagnostics;

LocalEnvFile.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFleetPulse(builder.Configuration);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is TransientTelemetryException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "El servicio de telemetría no está disponible en este momento."
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "Error inesperado." });
    });
});

app.UseMiddleware<ApiKeyMiddleware>();
app.MapHealthChecks("/health");

app.MapPost("/api/telemetry/batches", async (
    IngestBatchRequest request,
    IngestTelemetryBatchHandler handler,
    CancellationToken cancellationToken) =>
{
    var events = request.Events
        .Select(item => new IncomingTelemetryEvent(
            item.EventId,
            item.VehicleId,
            item.RecordedAt,
            item.Latitude,
            item.Longitude,
            item.SequenceNumber))
        .ToArray();

    var result = await handler.HandleAsync(events, cancellationToken);
    return result.IsAccepted
        ? Results.Accepted(value: new { accepted = result.AcceptedCount })
        : Results.BadRequest(new { errors = result.Errors });
});

app.Run();

public partial class Program;
