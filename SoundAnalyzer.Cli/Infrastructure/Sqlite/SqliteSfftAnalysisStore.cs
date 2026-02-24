using System.Globalization;
using Microsoft.Data.Sqlite;
using SFFTAnalyzer.Core.Application.Ports;
using SFFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal sealed class SqliteSfftAnalysisStore : ISfftAnalysisPointWriter, IDisposable
{
    private readonly string dbFilePath;
    private readonly string tableName;
    private readonly SqliteConflictMode conflictMode;
    private readonly int binCount;
    private readonly bool deleteCurrent;

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;
    private SqliteCommand? insertCommand;
    private bool completed;

    public SqliteSfftAnalysisStore(
        string dbFilePath,
        string tableName,
        SqliteConflictMode conflictMode,
        int binCount,
        bool deleteCurrent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        this.dbFilePath = dbFilePath;
        this.tableName = tableName;
        this.conflictMode = conflictMode;
        this.binCount = binCount;
        this.deleteCurrent = deleteCurrent;
    }

    public void Initialize()
    {
        if (connection is not null)
        {
            return;
        }

        connection = new SqliteConnection(BuildConnectionString(dbFilePath));
        connection.Open();

        transaction = connection.BeginTransaction();

        if (deleteCurrent)
        {
            DropTableIfExists(connection, transaction, tableName);
        }
        else if (TableExists(connection, transaction, tableName))
        {
            int existingBinCount = CountExistingBinColumns(connection, transaction, tableName);
            if (existingBinCount != binCount)
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, expected={binCount}, actual={existingBinCount}");
                throw new CliException(CliErrorCode.SfftTableBinCountMismatch, detail);
            }
        }

        CreateTableIfNeeded(connection, transaction, tableName, binCount);
        CreateIndexesIfNeeded(connection, transaction, tableName);

        insertCommand = BuildInsertCommand(connection, transaction, tableName, conflictMode, binCount);
    }

    public void Write(SfftAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        if (point.Bins.Count != binCount)
        {
            throw new ArgumentException("Point bin-count does not match configured bin-count.", nameof(point));
        }

        SqliteCommand command = insertCommand
            ?? throw new InvalidOperationException("SqliteSfftAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        command.Parameters["$name"].Value = point.Name;
        command.Parameters["$ch"].Value = point.Channel;
        command.Parameters["$window"].Value = point.WindowMs;
        command.Parameters["$ms"].Value = point.Ms;

        for (int i = 0; i < binCount; i++)
        {
            string parameterName = GetBinParameterName(i + 1);
            command.Parameters[parameterName].Value = point.Bins[i];
        }

        command.Parameters["$createAt"].Value = nowMs;
        command.Parameters["$modifiedAt"].Value = nowMs;

        _ = command.ExecuteNonQuery();
    }

    public void Complete()
    {
        SqliteTransaction? currentTransaction = transaction;
        if (currentTransaction is null)
        {
            return;
        }

        currentTransaction.Commit();
        completed = true;
    }

    public void Dispose()
    {
        try
        {
            if (!completed)
            {
                transaction?.Rollback();
            }
        }
        finally
        {
            insertCommand?.Dispose();
            transaction?.Dispose();
            connection?.Dispose();
        }
    }

    private static void DropTableIfExists(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};");
        _ = command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        _ = command.Parameters.AddWithValue("$name", tableName);

        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static int CountExistingBinColumns(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"PRAGMA table_info({QuoteIdentifier(tableName)});");

        int count = 0;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string columnName = reader.GetString(1);
            if (columnName.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static void CreateTableIfNeeded(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        int binCount)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string quotedTable = QuoteIdentifier(tableName);

        string[] binColumns = new string[binCount];
        for (int i = 0; i < binCount; i++)
        {
            string columnName = QuoteIdentifier(GetBinColumnName(i + 1));
            binColumns[i] = string.Create(CultureInfo.InvariantCulture, $"{columnName} REAL NOT NULL");
        }

        string joinedBinColumns = string.Join(",\n  ", binColumns);

        string sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            CREATE TABLE IF NOT EXISTS {quotedTable} (
              "idx" INTEGER PRIMARY KEY AUTOINCREMENT,
              "name" TEXT NOT NULL,
              "ch" INTEGER NOT NULL,
              "window" INTEGER NOT NULL,
              "ms" INTEGER NOT NULL,
              {joinedBinColumns},
              "create_at" INTEGER NOT NULL,
              "modified_at" INTEGER NOT NULL,
              UNIQUE("name", "ch", "window", "ms")
            );
            """);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static void CreateIndexesIfNeeded(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        string quotedTable = QuoteIdentifier(tableName);
        string safePrefix = tableName.Replace("-", "_", StringComparison.Ordinal);

        string[] statements =
        {
            BuildIndexStatement(safePrefix, "name", quotedTable, "name"),
            BuildIndexStatement(safePrefix, "name_ch", quotedTable, "name", "ch"),
            BuildIndexStatement(safePrefix, "name_ms", quotedTable, "name", "ms"),
            BuildIndexStatement(safePrefix, "name_ch_ms", quotedTable, "name", "ch", "ms"),
        };

        for (int i = 0; i < statements.Length; i++)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statements[i];
            _ = command.ExecuteNonQuery();
        }
    }

    private static string BuildIndexStatement(string prefix, string suffix, string quotedTable, params string[] columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(quotedTable);
        ArgumentNullException.ThrowIfNull(columns);

        string indexName = QuoteIdentifier(string.Create(CultureInfo.InvariantCulture, $"IX_{prefix}_{suffix}"));
        string joinedColumns = string.Join(", ", columns.Select(column => QuoteIdentifier(column)));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"CREATE INDEX IF NOT EXISTS {indexName} ON {quotedTable} ({joinedColumns});");
    }

    private static SqliteCommand BuildInsertCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        SqliteConflictMode conflictMode,
        int binCount)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string quotedTable = QuoteIdentifier(tableName);

        List<string> columns =
        [
            QuoteIdentifier("name"),
            QuoteIdentifier("ch"),
            QuoteIdentifier("window"),
            QuoteIdentifier("ms"),
        ];

        List<string> values = ["$name", "$ch", "$window", "$ms"];

        for (int i = 0; i < binCount; i++)
        {
            int index = i + 1;
            columns.Add(QuoteIdentifier(GetBinColumnName(index)));
            values.Add(GetBinParameterName(index));
        }

        columns.Add(QuoteIdentifier("create_at"));
        columns.Add(QuoteIdentifier("modified_at"));
        values.Add("$createAt");
        values.Add("$modifiedAt");

        string insertSql = string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO {quotedTable} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})");

        string sql = conflictMode switch
        {
            SqliteConflictMode.Upsert => string.Concat(
                insertSql,
                " ON CONFLICT(\"name\", \"ch\", \"window\", \"ms\") DO UPDATE SET ",
                BuildUpsertSetClause(binCount),
                ";"),
            SqliteConflictMode.SkipDuplicate => string.Concat(
                insertSql,
                " ON CONFLICT(\"name\", \"ch\", \"window\", \"ms\") DO NOTHING;"),
            _ => string.Concat(insertSql, ";"),
        };

        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        _ = command.Parameters.Add("$name", SqliteType.Text);
        _ = command.Parameters.Add("$ch", SqliteType.Integer);
        _ = command.Parameters.Add("$window", SqliteType.Integer);
        _ = command.Parameters.Add("$ms", SqliteType.Integer);

        for (int i = 0; i < binCount; i++)
        {
            _ = command.Parameters.Add(GetBinParameterName(i + 1), SqliteType.Real);
        }

        _ = command.Parameters.Add("$createAt", SqliteType.Integer);
        _ = command.Parameters.Add("$modifiedAt", SqliteType.Integer);
        command.Prepare();

        return command;
    }

    private static string BuildUpsertSetClause(int binCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        List<string> assignments = new(binCount + 1);
        for (int i = 0; i < binCount; i++)
        {
            string quotedColumn = QuoteIdentifier(GetBinColumnName(i + 1));
            assignments.Add(string.Create(CultureInfo.InvariantCulture, $"{quotedColumn} = excluded.{quotedColumn}"));
        }

        assignments.Add("\"modified_at\" = excluded.\"modified_at\"");
        return string.Join(", ", assignments);
    }

    private static string GetBinColumnName(int oneBasedIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(oneBasedIndex);
        return string.Create(CultureInfo.InvariantCulture, $"bin{oneBasedIndex:D3}");
    }

    private static string GetBinParameterName(int oneBasedIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(oneBasedIndex);
        return string.Create(CultureInfo.InvariantCulture, $"$bin{oneBasedIndex:D3}");
    }

    private static string BuildConnectionString(string dbFilePath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = false,
            Pooling = false,
        };

        return builder.ToString();
    }

    private static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        string escaped = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture, $"\"{escaped}\"");
    }
}
#pragma warning restore CA2100
