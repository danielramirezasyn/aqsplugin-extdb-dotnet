using System.Security.Cryptography;
using System.Text;
using AqsPluginExtDb.Core.Options;

namespace AqsPluginExtDb.Core.Security;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, PluginOptions options)
{
    private const string HeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) || !IsValidKey(provided.ToString(), options.ApiKey))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsValidKey(string provided, string expected)
    {
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "error",
            error_code = "UNAUTHORIZED",
            error_message = "Missing or invalid X-API-Key header."
        });
    }
}
