using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

internal static class SqliteBatchSizeCalculator
{
    private const int DefaultMaxVariableNumber = 999;
    private const string MaxVariableNumberPrefix = "MAX_VARIABLE_NUMBER=";

    public static int ResolveEffectiveBatchRowCount(
        SqliteConnection connection,
        int requestedBatchRowCount,
        int columnsPerRow)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedBatchRowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnsPerRow);

        int maxVariables = ReadMaxVariableNumber(connection);
        int maxRowsByVariableLimit = Math.Max(1, maxVariables / columnsPerRow);
        return Math.Min(requestedBatchRowCount, maxRowsByVariableLimit);
    }

    private static int ReadMaxVariableNumber(SqliteConnection connection)
    {
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA compile_options;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string option = reader.GetString(0);
                if (!option.StartsWith(MaxVariableNumberPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string value = option[MaxVariableNumberPrefix.Length..];
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                    && parsed > 0)
                {
                    return parsed;
                }
            }
        }
        catch (SqliteException)
        {
            // Fallback for environments that do not expose compile options.
        }

        return DefaultMaxVariableNumber;
    }
}
