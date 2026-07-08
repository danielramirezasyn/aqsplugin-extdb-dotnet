using AqsPluginExtDb.Core.Crypto;
using AqsPluginExtDb.Core.Storage;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Models;

namespace AqsPluginExtDb.Endpoints;

public static class SetupEndpoints
{
    public static void MapSetupEndpoints(this WebApplication app)
    {
        app.MapPost("/setup", PostSetupAsync);
        app.MapGet("/setup", GetSetupAsync);
        app.MapDelete("/setup/{alias}", DeleteSetupAsync);
    }

    private static async Task<IResult> PostSetupAsync(
        SetupRequest request,
        DriverRegistry drivers,
        CryptoService crypto,
        ConnectionStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
        {
            return Results.BadRequest(new { status = "error", error_code = "VALIDATION_ERROR", error_message = "alias is required." });
        }

        if (!drivers.TryGet(request.DbType, out _))
        {
            return Results.BadRequest(new
            {
                status = "error",
                error_code = "UNSUPPORTED_DRIVER",
                error_message = $"db_type '{request.DbType}' is not supported. Supported: {string.Join(", ", drivers.SupportedDrivers)}"
            });
        }

        string encryptedPassword = crypto.Encrypt(request.Password);

        var connection = new ConnectionAlias(
            request.Alias,
            request.DbType,
            request.Host,
            request.Port,
            request.Database,
            request.User,
            encryptedPassword,
            request.DriverOptions);

        await store.UpsertAsync(connection, ct);

        return Results.Ok(SetupResponseItem.FromConnectionAlias(connection));
    }

    private static async Task<IResult> GetSetupAsync(ConnectionStore store, CancellationToken ct)
    {
        var all = await store.GetAllAsync(ct);
        return Results.Ok(all.Select(SetupResponseItem.FromConnectionAlias));
    }

    private static async Task<IResult> DeleteSetupAsync(string alias, ConnectionStore store, CancellationToken ct)
    {
        bool removed = await store.DeleteAsync(alias, ct);
        return removed
            ? Results.Ok(new { status = "ok" })
            : Results.NotFound(new { status = "error", error_code = "ALIAS_NOT_FOUND", error_message = $"Alias '{alias}' is not registered." });
    }
}
