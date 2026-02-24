using System.Net;
using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal static class PostgresSshTunnelFactory
{
    public static PostgresSshTunnel CreateTunnel(PostgresSshOptions sshOptions, string destinationHost, int destinationPort)
    {
        ArgumentNullException.ThrowIfNull(sshOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationHost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationPort);

        string resolvedPrivateKeyPath = Path.GetFullPath(sshOptions.PrivateKeyPath);
        if (!File.Exists(resolvedPrivateKeyPath))
        {
            throw new CliException(CliErrorCode.PostgresCredentialFileNotFound, resolvedPrivateKeyPath);
        }

        string resolvedKnownHostsPath = Path.GetFullPath(sshOptions.KnownHostsPath);
        if (!File.Exists(resolvedKnownHostsPath))
        {
            throw new CliException(CliErrorCode.PostgresCredentialFileNotFound, resolvedKnownHostsPath);
        }

        PrivateKeyFile keyFile;
        try
        {
            keyFile = new PrivateKeyFile(resolvedPrivateKeyPath);
        }
        catch (Exception ex) when (ex is SshException or IOException or UnauthorizedAccessException)
        {
            throw new CliException(CliErrorCode.PostgresSshTunnelFailed, ex.Message, ex);
        }

        ConnectionInfo connectionInfo = new(
            sshOptions.Host,
            sshOptions.Port,
            sshOptions.User,
            new PrivateKeyAuthenticationMethod(sshOptions.User, keyFile));

        SshClient sshClient = new(connectionInfo);
        sshClient.HostKeyReceived += (_, eventArgs) =>
        {
            eventArgs.CanTrust = KnownHostsVerifier.IsTrusted(
                resolvedKnownHostsPath,
                sshOptions.Host,
                sshOptions.Port,
                eventArgs.HostKeyName,
                eventArgs.HostKey);
        };

        try
        {
            sshClient.Connect();
            if (!sshClient.IsConnected)
            {
                throw new CliException(CliErrorCode.PostgresSshTunnelFailed, "Failed to establish SSH connection.");
            }

            int localPort = FindAvailableLoopbackPort();
            ForwardedPortLocal forwardedPort = new(
                "127.0.0.1",
                (uint)localPort,
                destinationHost,
                (uint)destinationPort);

            sshClient.AddForwardedPort(forwardedPort);
            forwardedPort.Start();
            if (!forwardedPort.IsStarted)
            {
                throw new CliException(CliErrorCode.PostgresSshTunnelFailed, "Failed to start SSH local forwarding.");
            }

            return new PostgresSshTunnel(sshClient, forwardedPort, "127.0.0.1", localPort);
        }
        catch (SshConnectionException ex)
        {
            sshClient.Dispose();
            throw new CliException(CliErrorCode.PostgresSshTunnelFailed, ex.Message, ex);
        }
        catch (SshAuthenticationException ex)
        {
            sshClient.Dispose();
            throw new CliException(CliErrorCode.PostgresSshTunnelFailed, ex.Message, ex);
        }
        catch (SshOperationTimeoutException ex)
        {
            sshClient.Dispose();
            throw new CliException(CliErrorCode.PostgresSshTunnelFailed, ex.Message, ex);
        }
        catch (CliException)
        {
            sshClient.Dispose();
            throw;
        }
    }

    private static int FindAvailableLoopbackPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint? endPoint = listener.LocalEndpoint as IPEndPoint;
        return endPoint?.Port ?? throw new InvalidOperationException("Could not allocate local port.");
    }
}
