using System.Globalization;
using Npgsql;
using SoundAnalyzer.Cli.Infrastructure.Postgres;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Postgres;

internal static class PostgresTestEnvironment
{
    private const string HostEnv = "SOUNDANALYZER_PG_HOST";
    private const string PortEnv = "SOUNDANALYZER_PG_PORT";
    private const string DatabaseEnv = "SOUNDANALYZER_PG_DB";
    private const string UserEnv = "SOUNDANALYZER_PG_USER";
    private const string PasswordEnv = "SOUNDANALYZER_PG_PASSWORD";

    public static PostgresConnectionOptions? GetConnectionOptionsOrNull()
    {
        string? host = Environment.GetEnvironmentVariable(HostEnv);
        string? database = Environment.GetEnvironmentVariable(DatabaseEnv);
        string? user = Environment.GetEnvironmentVariable(UserEnv);

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(user))
        {
            return null;
        }

        string? portText = Environment.GetEnvironmentVariable(PortEnv);
        int port = 5432;
        if (!string.IsNullOrWhiteSpace(portText))
        {
            if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port <= 0)
            {
                return null;
            }
        }

        string? password = Environment.GetEnvironmentVariable(PasswordEnv);
        return new PostgresConnectionOptions(
            host,
            port,
            database,
            user,
            password,
            sslCertPath: null,
            sslKeyPath: null,
            sslRootCertPath: null);
    }

    public static NpgsqlConnection OpenConnection(PostgresConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.User,
            Timeout = 15,
            CommandTimeout = 120,
        };

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            builder.Password = options.Password;
        }

        NpgsqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    public static string CreateRandomTableName(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}_{Guid.NewGuid():N}");
    }
}
