using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Models;
using MySqlConnector;
using Xunit;

namespace AqsPluginExtDb.Tests.Drivers;

public class MySqlDriverTests
{
    [Fact]
    public void BuildConnectionString_IncludesHostPortDatabaseAndPoolSettings()
    {
        var driver = new MySqlDriver();
        var alias = new ConnectionAlias("core", "mysql", "10.0.2.10", 3306, "ordersdb", "svc_user", "ENC:unused", null);
        var pool = new PoolOptions { Enabled = true, MinSize = 3, MaxSize = 15, TimeoutSeconds = 20 };

        string connectionString = driver.BuildConnectionString(alias, "pw123", pool);
        var builder = new MySqlConnectionStringBuilder(connectionString);

        Assert.Equal("10.0.2.10", builder.Server);
        Assert.Equal(3306u, builder.Port);
        Assert.Equal("ordersdb", builder.Database);
        Assert.Equal("svc_user", builder.UserID);
        Assert.Equal("pw123", builder.Password);
        Assert.Equal(3u, builder.MinimumPoolSize);
        Assert.Equal(15u, builder.MaximumPoolSize);
    }

    [Fact]
    public void BuildConnectionString_AppliesDriverOptionsOverrides()
    {
        var driver = new MySqlDriver();
        var alias = new ConnectionAlias(
            "core", "mysql", "host", 3306, "db", "user", "ENC:unused",
            new Dictionary<string, string> { ["SslMode"] = "None" });
        var pool = new PoolOptions();

        string connectionString = driver.BuildConnectionString(alias, "pw", pool);
        var builder = new MySqlConnectionStringBuilder(connectionString);

        Assert.Equal(MySqlSslMode.None, builder.SslMode);
    }

    [Fact]
    public void DriverName_IsMySql()
    {
        Assert.Equal("mysql", new MySqlDriver().DriverName);
    }
}
