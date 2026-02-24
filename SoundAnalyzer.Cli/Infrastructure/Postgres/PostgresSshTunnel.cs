using Renci.SshNet;
using Renci.SshNet.Common;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal sealed class PostgresSshTunnel : IDisposable
{
    private readonly SshClient sshClient;
    private readonly ForwardedPortLocal forwardedPort;
    private bool disposed;

    public PostgresSshTunnel(SshClient sshClient, ForwardedPortLocal forwardedPort, string localHost, int localPort)
    {
        this.sshClient = sshClient ?? throw new ArgumentNullException(nameof(sshClient));
        this.forwardedPort = forwardedPort ?? throw new ArgumentNullException(nameof(forwardedPort));
        ArgumentException.ThrowIfNullOrWhiteSpace(localHost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(localPort);

        LocalHost = localHost;
        LocalPort = localPort;
    }

    public string LocalHost { get; }

    public int LocalPort { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (forwardedPort.IsStarted)
            {
                forwardedPort.Stop();
            }
        }
        catch (SshException)
        {
            // Best-effort stop while tearing down.
        }

        try
        {
            if (sshClient.IsConnected)
            {
                sshClient.Disconnect();
            }
        }
        catch (SshException)
        {
            // Best-effort disconnect while tearing down.
        }

        forwardedPort.Dispose();
        sshClient.Dispose();
    }
}
