using FleetPulse.Api;
using FleetPulse.Api.Contracts;
using FleetPulse.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;

LocalEnvFile.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFleetPulse(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FleetPulse",
        Version = "v1"
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Valor de TELEMETRY_API_KEY"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

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
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FleetPulse");
    options.RoutePrefix = "swagger";
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.MapGet("/api/vehicles", async (
    int? limit,
    IFleetQuery fleetQuery,
    CancellationToken cancellationToken) =>
{
    var take = limit ?? FleetQueryLimits.DefaultVehicles;
    if (!FleetQueryLimits.IsValidVehicleLimit(take))
    {
        return Results.BadRequest(new
        {
            error = $"El límite debe estar entre 1 y {FleetQueryLimits.MaxVehicles}."
        });
    }

    var vehicles = await fleetQuery.ListCurrentAsync(take, cancellationToken);
    return Results.Ok(vehicles);
});

app.MapGet("/api/vehicles/{vehicleId:guid}/positions", async (
    Guid vehicleId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit,
    IFleetQuery fleetQuery,
    TimeProvider clock,
    CancellationToken cancellationToken) =>
{
    var take = limit ?? FleetQueryLimits.DefaultPositions;
    if (!FleetQueryLimits.IsValidPositionLimit(take))
    {
        return Results.BadRequest(new
        {
            error = $"El límite debe estar entre 1 y {FleetQueryLimits.MaxPositions}."
        });
    }

    var rangeEnd = to ?? clock.GetUtcNow();
    var rangeStart = from ?? rangeEnd.AddHours(-24);
    if (!FleetQueryLimits.IsValidRange(rangeStart, rangeEnd))
        return Results.BadRequest(new { error = "El rango de fechas no es válido." });

    var track = await fleetQuery.ListPositionsAsync(vehicleId, rangeStart, rangeEnd, take, cancellationToken);
    return track.Exists
        ? Results.Ok(track.Positions)
        : Results.NotFound(new { error = "El vehículo no existe." });
});

app.MapPost("/api/agent/questions", async (
    AgentQuestionRequest request,
    IFleetAgent agent,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "La pregunta es obligatoria." });

    var reply = await agent.AskAsync(request.Question, cancellationToken);
    return Results.Ok(reply);
});

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

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "No encontrado." });
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
        return;
    }

    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.WriteAsJsonAsync(new
    {
        service = "FleetPulse",
        health = "/health"
    });
});

app.Run();

public partial class Program;
