using Microsoft.Data.Sqlite;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

internal static class SqliteJournalModeConfigurator
{
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
}
