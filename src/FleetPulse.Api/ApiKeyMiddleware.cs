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
        if (!RequiresApiKey(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var provided = ReadApiKey(context);
        if (provided is null || !Matches(provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "No autorizado." });
            return;
        }

        await _next(context);
    }

    private static bool RequiresApiKey(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs");

    private static string? ReadApiKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var header)
            && !string.IsNullOrWhiteSpace(header.ToString()))
        {
            return header.ToString();
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            const string bearer = "Bearer ";
            var value = authorization.ToString();
            if (value.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(value[bearer.Length..]))
            {
                return value[bearer.Length..].Trim();
            }
        }

        if (context.Request.Path.StartsWithSegments("/hubs")
            && context.Request.Query.TryGetValue("access_token", out var accessToken)
            && !string.IsNullOrWhiteSpace(accessToken.ToString()))
        {
            return accessToken.ToString();
        }

        return null;
    }

    private bool Matches(string provided)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(providedHash, _expectedHash);
    }
}
