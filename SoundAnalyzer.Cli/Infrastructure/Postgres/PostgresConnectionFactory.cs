using Npgsql;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal static class PostgresConnectionFactory
{
    public static PostgresSession OpenSession(PostgresConnectionOptions connectionOptions, PostgresSshOptions? sshOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);

        PostgresSshTunnel? tunnel = null;
        string host = connectionOptions.Host;
        int port = connectionOptions.Port;

        if (sshOptions is not null)
        {
            tunnel = PostgresSshTunnelFactory.CreateTunnel(sshOptions, connectionOptions.Host, connectionOptions.Port);
            host = tunnel.LocalHost;
            port = tunnel.LocalPort;
        }

        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = host,
            Port = port,
            Database = connectionOptions.Database,
            Username = connectionOptions.User,
            Timeout = 15,
            CommandTimeout = 120,
        };

        if (!string.IsNullOrWhiteSpace(connectionOptions.Password))
        {
            builder.Password = connectionOptions.Password;
        }

        ConfigureSsl(builder, connectionOptions);

        try
        {
            NpgsqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            return new PostgresSession(connection, tunnel);
        }
        catch
        {
            tunnel?.Dispose();
            throw;
        }
    }

    private static void ConfigureSsl(NpgsqlConnectionStringBuilder builder, PostgresConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        string? rootCertPath = ResolveOptionalExistingPath(options.SslRootCertPath);
        string? certPath = ResolveOptionalExistingPath(options.SslCertPath);
        string? keyPath = ResolveOptionalExistingPath(options.SslKeyPath);

        bool useTls = rootCertPath is not null || certPath is not null || keyPath is not null;
        if (!useTls)
        {
            return;
        }

        builder.SslMode = rootCertPath is null ? SslMode.Require : SslMode.VerifyCA;
        if (rootCertPath is not null)
        {
            builder.RootCertificate = rootCertPath;
        }

        if (certPath is not null)
        {
            builder.SslCertificate = certPath;
        }

        if (keyPath is not null)
        {
            builder.SslKey = keyPath;
        }
    }

    private static string? ResolveOptionalExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string resolvedPath = Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new CliException(CliErrorCode.PostgresCredentialFileNotFound, resolvedPath);
        }

        return resolvedPath;
    }
}
