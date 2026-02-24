namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal sealed class PostgresConnectionOptions
{
    public PostgresConnectionOptions(
        string host,
        int port,
        string database,
        string user,
        string? password,
        string? sslCertPath,
        string? sslKeyPath,
        string? sslRootCertPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        Host = host.Trim();
        Port = port;
        Database = database.Trim();
        User = user.Trim();
        Password = string.IsNullOrWhiteSpace(password) ? null : password;
        SslCertPath = string.IsNullOrWhiteSpace(sslCertPath) ? null : sslCertPath.Trim();
        SslKeyPath = string.IsNullOrWhiteSpace(sslKeyPath) ? null : sslKeyPath.Trim();
        SslRootCertPath = string.IsNullOrWhiteSpace(sslRootCertPath) ? null : sslRootCertPath.Trim();
    }

    public string Host { get; }

    public int Port { get; }

    public string Database { get; }

    public string User { get; }

    public string? Password { get; }

    public string? SslCertPath { get; }

    public string? SslKeyPath { get; }

    public string? SslRootCertPath { get; }
}
