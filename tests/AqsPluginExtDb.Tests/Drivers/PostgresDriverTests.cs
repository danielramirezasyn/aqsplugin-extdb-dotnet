using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Models;
using Npgsql;
using Xunit;

namespace AqsPluginExtDb.Tests.Drivers;

public class PostgresDriverTests
{
    [Fact]
    public void BuildConnectionString_IncludesHostPortDatabaseAndPoolSettings()
    {
        var driver = new PostgresDriver();
        var alias = new ConnectionAlias("core", "postgresql", "10.0.3.20", 5432, "analytics", "ro_user", "ENC:unused", null);
        var pool = new PoolOptions { Enabled = true, MinSize = 1, MaxSize = 5, TimeoutSeconds = 15 };

        string connectionString = driver.BuildConnectionString(alias, "pw456", pool);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("10.0.3.20", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("analytics", builder.Database);
        Assert.Equal("ro_user", builder.Username);
        Assert.Equal("pw456", builder.Password);
        Assert.Equal(1, builder.MinPoolSize);
        Assert.Equal(5, builder.MaxPoolSize);
    }

    [Fact]
    public void BuildConnectionString_AppliesDriverOptionsOverrides()
    {
        var driver = new PostgresDriver();
        var alias = new ConnectionAlias(
            "core", "postgresql", "host", 5432, "db", "user", "ENC:unused",
            new Dictionary<string, string> { ["SslMode"] = "Disable" });
        var pool = new PoolOptions();

        string connectionString = driver.BuildConnectionString(alias, "pw", pool);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal(SslMode.Disable, builder.SslMode);
    }

    [Fact]
    public void DriverName_IsPostgresql()
    {
        Assert.Equal("postgresql", new PostgresDriver().DriverName);
    }
}
