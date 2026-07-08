using AqsPluginExtDb.Core.Options;
using AqsPluginExtDb.Drivers;
using AqsPluginExtDb.Models;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AqsPluginExtDb.Tests.Drivers;

public class SqlServerDriverTests
{
    [Fact]
    public void BuildConnectionString_IncludesHostPortDatabaseAndPoolSettings()
    {
        var driver = new SqlServerDriver();
        var alias = new ConnectionAlias("core", "sqlserver", "10.0.1.45", 1433, "CoreBancario", "apireader", "ENC:unused", null);
        var pool = new PoolOptions { Enabled = true, MinSize = 2, MaxSize = 10, TimeoutSeconds = 30 };

        string connectionString = driver.BuildConnectionString(alias, "s3cret", pool);
        var builder = new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("10.0.1.45,1433", builder.DataSource);
        Assert.Equal("CoreBancario", builder.InitialCatalog);
        Assert.Equal("apireader", builder.UserID);
        Assert.Equal("s3cret", builder.Password);
        Assert.Equal(2, builder.MinPoolSize);
        Assert.Equal(10, builder.MaxPoolSize);
    }

    [Fact]
    public void BuildConnectionString_AppliesDriverOptionsOverrides()
    {
        var driver = new SqlServerDriver();
        var alias = new ConnectionAlias(
            "core", "sqlserver", "host", 1433, "db", "user", "ENC:unused",
            new Dictionary<string, string> { ["Encrypt"] = "false" });
        var pool = new PoolOptions();

        string connectionString = driver.BuildConnectionString(alias, "pw", pool);
        var builder = new SqlConnectionStringBuilder(connectionString);

        Assert.False(builder.Encrypt);
    }

    [Fact]
    public void DriverName_IsSqlServer()
    {
        Assert.Equal("sqlserver", new SqlServerDriver().DriverName);
    }
}
