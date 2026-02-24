using System.Globalization;
using System.Net;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal static class KnownHostsVerifier
{
    public static bool IsTrusted(
        string knownHostsPath,
        string host,
        int port,
        string hostKeyAlgorithm,
        byte[] hostKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knownHostsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostKeyAlgorithm);
        ArgumentNullException.ThrowIfNull(hostKey);

        string resolvedPath = Path.GetFullPath(knownHostsPath);
        if (!File.Exists(resolvedPath))
        {
            return false;
        }

        string expectedKey = Convert.ToBase64String(hostKey);
        string normalizedHost = NormalizeHost(host);

        foreach (string rawLine in File.ReadAllLines(resolvedPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('@'))
            {
                // Skip markers such as @cert-authority.
                continue;
            }

            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length < 3)
            {
                continue;
            }

            string hostPatternToken = tokens[0];
            if (hostPatternToken.StartsWith("|1|", StringComparison.Ordinal))
            {
                // Hashed hosts cannot be validated without the HMAC key from OpenSSH internals.
                continue;
            }

            if (!tokens[1].Equals(hostKeyAlgorithm, StringComparison.Ordinal))
            {
                continue;
            }

            if (!tokens[2].Equals(expectedKey, StringComparison.Ordinal))
            {
                continue;
            }

            string[] hostPatterns = hostPatternToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < hostPatterns.Length; i++)
            {
                if (HostPatternMatches(hostPatterns[i], normalizedHost, port))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HostPatternMatches(string pattern, string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

        if (TryParseBracketHostAndPort(pattern, out string bracketHost, out int bracketPort))
        {
            return bracketPort == port && HostsEqual(bracketHost, host);
        }

        // Non-bracket host patterns in known_hosts implicitly target port 22.
        return port == 22 && HostsEqual(pattern, host);
    }

    private static bool TryParseBracketHostAndPort(string pattern, out string host, out int port)
    {
        host = string.Empty;
        port = default;

        int closeBracketIndex = pattern.IndexOf(']');
        if (pattern.Length == 0
            || pattern[0] != '['
            || closeBracketIndex <= 1
            || closeBracketIndex + 2 >= pattern.Length
            || pattern[closeBracketIndex + 1] != ':')
        {
            return false;
        }

        string parsedHost = pattern[1..closeBracketIndex];
        string parsedPortText = pattern[(closeBracketIndex + 2)..];
        if (!int.TryParse(parsedPortText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedPort)
            || parsedPort <= 0)
        {
            return false;
        }

        host = NormalizeHost(parsedHost);
        port = parsedPort;
        return true;
    }

    private static bool HostsEqual(string left, string right)
    {
        return NormalizeHost(left).Equals(NormalizeHost(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        string trimmed = host.Trim();
        return Uri.CheckHostName(trimmed) == UriHostNameType.IPv6
            ? IPAddress.Parse(trimmed).ToString()
            : trimmed;
    }
}
