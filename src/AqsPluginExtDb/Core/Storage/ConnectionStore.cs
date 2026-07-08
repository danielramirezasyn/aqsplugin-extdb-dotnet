using System.Text.Json;
using AqsPluginExtDb.Models;

namespace AqsPluginExtDb.Core.Storage;

/// <summary>
/// Persists registered connection aliases to a JSON file (mounted as a Docker volume),
/// serializing concurrent access with an async lock and writing atomically via a temp file.
/// </summary>
public sealed class ConnectionStore(string filePath, ILogger<ConnectionStore> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<IReadOnlyList<ConnectionAlias>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await LoadAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ConnectionAlias?> GetAsync(string alias, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(ConnectionAlias connection, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = (await LoadAsync(ct)).ToList();
            int index = all.FindIndex(c => string.Equals(c.Alias, connection.Alias, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                all[index] = connection;
            }
            else
            {
                all.Add(connection);
            }

            await SaveAsync(all, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string alias, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = (await LoadAsync(ct)).ToList();
            int removed = all.RemoveAll(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await SaveAsync(all, ct);
            }

            return removed > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ConnectionAlias>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var loaded = await JsonSerializer.DeserializeAsync<List<ConnectionAlias>>(stream, cancellationToken: ct);
            return loaded ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse connections store at {FilePath}; treating as empty.", filePath);
            return [];
        }
    }

    private async Task SaveAsync(List<ConnectionAlias> connections, CancellationToken ct)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, connections, SerializerOptions, ct);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
