namespace AqsPluginExtDb.Core.Options;

public sealed class PoolOptions
{
    public bool Enabled { get; init; } = true;
    public int MinSize { get; init; } = 2;
    public int MaxSize { get; init; } = 10;
    public int TimeoutSeconds { get; init; } = 30;

    public static PoolOptions FromEnvironment(IConfiguration configuration) => new()
    {
        Enabled = !bool.TryParse(configuration["POOL_ENABLED"], out bool enabled) || enabled,
        MinSize = int.TryParse(configuration["POOL_MIN_SIZE"], out int min) ? min : 2,
        MaxSize = int.TryParse(configuration["POOL_MAX_SIZE"], out int max) ? max : 10,
        TimeoutSeconds = int.TryParse(configuration["POOL_TIMEOUT"], out int timeout) ? timeout : 30
    };
}
