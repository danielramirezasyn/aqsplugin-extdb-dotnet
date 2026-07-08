using System.Text.Json.Serialization;
using AqsPluginExtDb.Core.Crypto;
using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Core.Security;
using AqsPluginExtDb.Core.Storage;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Endpoints;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var pluginOptions = PluginOptions.FromEnvironment(builder.Configuration);
var poolOptions = PoolOptions.FromEnvironment(builder.Configuration);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenAnyIP(pluginOptions.Port));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton(pluginOptions);
builder.Services.AddSingleton(poolOptions);
builder.Services.AddSingleton(new CryptoService(pluginOptions.EncryptionKey));

string connectionsFilePath = builder.Configuration["CONNECTIONS_FILE_PATH"] ?? "/data/connections.json";
builder.Services.AddSingleton(sp => new ConnectionStore(connectionsFilePath, sp.GetRequiredService<ILogger<ConnectionStore>>()));

builder.Services.AddSingleton<IDbDriver, SqlServerDriver>();
builder.Services.AddSingleton<IDbDriver, MySqlDriver>();
builder.Services.AddSingleton<IDbDriver, PostgresDriver>();
builder.Services.AddSingleton<DriverRegistry>();

var app = builder.Build();

// Global safety net: no unhandled exception should ever crash the process or return a
// non-JSON body — PL/SQL callers always get a parseable error envelope.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(feature?.Error, "Unhandled exception");

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "error",
            error_code = "INTERNAL_ERROR",
            error_message = "An unexpected error occurred.",
            rows_affected = 0,
            columns = Array.Empty<string>(),
            data = Array.Empty<object>(),
            execution_ms = 0
        });
    });
});

app.UseMiddleware<IpAllowlistMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapHealthEndpoints();
app.MapSetupEndpoints();
app.MapExecuteEndpoints();

app.Run();

public partial class Program
{
}
