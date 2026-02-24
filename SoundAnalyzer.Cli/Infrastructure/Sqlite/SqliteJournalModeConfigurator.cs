using Microsoft.Data.Sqlite;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal static class SqliteJournalModeConfigurator
{
    public static string Configure(SqliteConnection connection, bool fastMode)
    {
        ArgumentNullException.ThrowIfNull(connection);

        string mode = TryEnableWal(connection);
        if (fastMode)
        {
            TryExecutePragma(connection, SqliteFastPragma.SynchronousOff);
            TryExecutePragma(connection, SqliteFastPragma.LockingExclusive);
            TryExecutePragma(connection, SqliteFastPragma.TempStoreMemory);
            TryExecutePragma(connection, SqliteFastPragma.CacheSizeLarge);
        }

        return mode;
    }

    public static string TryEnableWal(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            object? scalar = command.ExecuteScalar();
            if (scalar is string mode && !string.IsNullOrWhiteSpace(mode))
            {
                return mode;
            }
        }
        catch (SqliteException)
        {
            // Some environments/filesystems cannot switch to WAL.
            // Fallback mode is accepted by design.
        }

        using SqliteCommand fallbackCommand = connection.CreateCommand();
        fallbackCommand.CommandText = "PRAGMA journal_mode;";
        object? fallbackScalar = fallbackCommand.ExecuteScalar();
        return fallbackScalar as string ?? string.Empty;
    }

    private static void TryExecutePragma(SqliteConnection connection, SqliteFastPragma pragma)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = pragma switch
            {
                SqliteFastPragma.SynchronousOff => "PRAGMA synchronous=OFF;",
                SqliteFastPragma.LockingExclusive => "PRAGMA locking_mode=EXCLUSIVE;",
                SqliteFastPragma.TempStoreMemory => "PRAGMA temp_store=MEMORY;",
                SqliteFastPragma.CacheSizeLarge => "PRAGMA cache_size=-262144;",
                _ => throw new ArgumentOutOfRangeException(nameof(pragma)),
            };
            _ = command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Best-effort tuning.
        }
    }

    private enum SqliteFastPragma
    {
        SynchronousOff = 0,
        LockingExclusive,
        TempStoreMemory,
        CacheSizeLarge,
    }
}
#pragma warning restore CA2100
