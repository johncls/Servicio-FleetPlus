using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FleetPulse.Api;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[] _expectedHash;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<TelemetryApiOptions> options)
    {
        _next = next;
        _expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ApiKey));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided)
            || !Matches(provided.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "No autorizado." });
            return;
        }

        await _next(context);
    }

    private bool Matches(string provided)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(providedHash, _expectedHash);
    }
}
