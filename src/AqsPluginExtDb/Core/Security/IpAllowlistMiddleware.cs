using AqsPluginExtDb.Core.Options;

namespace AqsPluginExtDb.Core.Security;

public sealed class IpAllowlistMiddleware(RequestDelegate next, PluginOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (options.AllowedNetworks.Count == 0)
        {
            await next(context);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        var normalizedIp = remoteIp is { IsIPv4MappedToIPv6: true } ? remoteIp.MapToIPv4() : remoteIp;

        if (normalizedIp is not null && options.AllowedNetworks.Any(network => network.Contains(normalizedIp)))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "error",
            error_code = "IP_NOT_ALLOWED",
            error_message = "Client IP is not in the allowlist."
        });
    }
}
