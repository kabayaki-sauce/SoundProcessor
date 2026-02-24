namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal sealed class PostgresSshOptions
{
    public PostgresSshOptions(
        string host,
        int port,
        string user,
        string privateKeyPath,
        string knownHostsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(knownHostsPath);

        Host = host.Trim();
        Port = port;
        User = user.Trim();
        PrivateKeyPath = privateKeyPath.Trim();
        KnownHostsPath = knownHostsPath.Trim();
    }

    public string Host { get; }

    public int Port { get; }

    public string User { get; }

    public string PrivateKeyPath { get; }

    public string KnownHostsPath { get; }
}
