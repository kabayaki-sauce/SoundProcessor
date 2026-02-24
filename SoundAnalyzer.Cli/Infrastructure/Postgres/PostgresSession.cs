using Npgsql;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal sealed class PostgresSession : IDisposable
{
    private readonly IDisposable? disposableDependency;
    private bool disposed;

    public PostgresSession(NpgsqlConnection connection, IDisposable? disposableDependency)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.disposableDependency = disposableDependency;
    }

    public NpgsqlConnection Connection { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Connection.Dispose();
        disposableDependency?.Dispose();
    }
}
