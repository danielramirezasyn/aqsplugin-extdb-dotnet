using System.Net;

namespace AqsPluginExtDb.Core.Options;

public sealed class PluginOptions
{
    public required string ApiKey { get; init; }
    public required string EncryptionKey { get; init; }
    public IReadOnlyList<IPNetwork> AllowedNetworks { get; init; } = [];
    public int Port { get; init; } = 8000;

    public static PluginOptions FromEnvironment(IConfiguration configuration)
    {
        string apiKey = configuration["PLUGIN_API_KEY"]
            ?? throw new InvalidOperationException("PLUGIN_API_KEY environment variable is required.");

        string encryptionKey = configuration["ENCRYPTION_KEY"]
            ?? throw new InvalidOperationException("ENCRYPTION_KEY environment variable is required.");

        string? allowedIpsRaw = configuration["ALLOWED_IPS"];
        List<IPNetwork> allowedNetworks = [];
        if (!string.IsNullOrWhiteSpace(allowedIpsRaw))
        {
            foreach (string entry in allowedIpsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                allowedNetworks.Add(ParseNetwork(entry));
            }
        }

        int port = int.TryParse(configuration["PORT"], out int parsedPort) ? parsedPort : 8000;

        return new PluginOptions
        {
            ApiKey = apiKey,
            EncryptionKey = encryptionKey,
            AllowedNetworks = allowedNetworks,
            Port = port
        };
    }

    private static IPNetwork ParseNetwork(string entry)
    {
        if (entry.Contains('/'))
        {
            return IPNetwork.Parse(entry);
        }

        var address = IPAddress.Parse(entry);
        int prefixLength = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        return new IPNetwork(address, prefixLength);
    }
}
