namespace AqsPluginExtDb.Drivers;

public sealed class DriverRegistry
{
    private readonly Dictionary<string, IDbDriver> _drivers;

    public DriverRegistry(IEnumerable<IDbDriver> drivers)
    {
        _drivers = drivers.ToDictionary(d => d.DriverName, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string dbType, out IDbDriver? driver) => _drivers.TryGetValue(dbType, out driver);

    public IReadOnlyCollection<string> SupportedDrivers => _drivers.Keys;
}
