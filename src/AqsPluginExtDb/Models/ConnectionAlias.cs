namespace AqsPluginExtDb.Models;

public sealed record ConnectionAlias(
    string Alias,
    string DbType,
    string Host,
    int Port,
    string Database,
    string User,
    string EncryptedPassword,
    Dictionary<string, string>? DriverOptions);
