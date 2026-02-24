using System.Globalization;
using Microsoft.Data.Sqlite;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal sealed class SqliteStftAnalysisStore : IStftAnalysisPointWriter, IDisposable
{
    private readonly string dbFilePath;
    private readonly string tableName;
    private readonly string anchorColumnName;
    private readonly SqliteConflictMode conflictMode;
    private readonly int binCount;
    private readonly bool deleteCurrent;

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;
    private SqliteCommand? insertCommand;
    private bool completed;

    public SqliteStftAnalysisStore(
        string dbFilePath,
        string tableName,
        string anchorColumnName,
        SqliteConflictMode conflictMode,
        int binCount,
        bool deleteCurrent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string normalizedAnchorColumnName = anchorColumnName.Trim().ToLowerInvariant();
        if (!normalizedAnchorColumnName.Equals("ms", StringComparison.Ordinal)
            && !normalizedAnchorColumnName.Equals("sample", StringComparison.Ordinal))
        {
            throw new ArgumentException("Anchor column must be 'ms' or 'sample'.", nameof(anchorColumnName));
        }

        this.dbFilePath = dbFilePath;
        this.tableName = tableName;
        this.anchorColumnName = normalizedAnchorColumnName;
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
        _ = SqliteJournalModeConfigurator.TryEnableWal(connection);

        transaction = connection.BeginTransaction();

        if (deleteCurrent)
        {
            DropTableIfExists(connection, transaction, tableName);
        }
        else if (TableExists(connection, transaction, tableName))
        {
            ExistingTableSchema schema = ReadExistingTableSchema(connection, transaction, tableName);
            if (schema.BinCount != binCount)
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, expected={binCount}, actual={schema.BinCount}");
                throw new CliException(CliErrorCode.StftTableBinCountMismatch, detail);
            }

            if (!schema.HasExpectedAnchorOnly(anchorColumnName))
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, expected-anchor={anchorColumnName}, actual-columns={string.Join(',', schema.Columns)}");
                throw new CliException(CliErrorCode.StftTableSchemaMismatch, detail);
            }
        }

        CreateTableIfNeeded(connection, transaction, tableName, anchorColumnName, binCount);
        CreateIndexesIfNeeded(connection, transaction, tableName, anchorColumnName);

        insertCommand = BuildInsertCommand(connection, transaction, tableName, anchorColumnName, conflictMode, binCount);
    }

    public void Write(StftAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        if (point.Bins.Count != binCount)
        {
            throw new ArgumentException("Point bin-count does not match configured bin-count.", nameof(point));
        }

        SqliteCommand command = insertCommand
            ?? throw new InvalidOperationException("SqliteStftAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        command.Parameters["$name"].Value = point.Name;
        command.Parameters["$ch"].Value = point.Channel;
        command.Parameters["$window"].Value = point.Window;
        command.Parameters["$anchor"].Value = point.Anchor;

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

    private static ExistingTableSchema ReadExistingTableSchema(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"PRAGMA table_info({QuoteIdentifier(tableName)});");

        List<string> columns = new();
        int binCount = 0;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string columnName = reader.GetString(1);
            columns.Add(columnName);
            if (columnName.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                binCount++;
            }
        }

        return new ExistingTableSchema(columns, binCount);
    }

    private static void CreateTableIfNeeded(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string anchorColumnName,
        int binCount)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string quotedTable = QuoteIdentifier(tableName);
        string quotedAnchorColumn = QuoteIdentifier(anchorColumnName);

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
              {quotedAnchorColumn} INTEGER NOT NULL,
              {joinedBinColumns},
              "create_at" INTEGER NOT NULL,
              "modified_at" INTEGER NOT NULL,
              UNIQUE("name", "ch", "window", {quotedAnchorColumn})
            );
            """);

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static void CreateIndexesIfNeeded(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string anchorColumnName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);

        string quotedTable = QuoteIdentifier(tableName);
        string safePrefix = tableName.Replace("-", "_", StringComparison.Ordinal);
        string anchorSuffix = anchorColumnName.Replace("-", "_", StringComparison.Ordinal);

        string[] statements =
        {
            BuildIndexStatement(safePrefix, "name", quotedTable, "name"),
            BuildIndexStatement(safePrefix, "name_ch", quotedTable, "name", "ch"),
            BuildIndexStatement(safePrefix, string.Create(CultureInfo.InvariantCulture, $"name_{anchorSuffix}"), quotedTable, "name", anchorColumnName),
            BuildIndexStatement(safePrefix, string.Create(CultureInfo.InvariantCulture, $"name_ch_{anchorSuffix}"), quotedTable, "name", "ch", anchorColumnName),
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
        string anchorColumnName,
        SqliteConflictMode conflictMode,
        int binCount)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string quotedTable = QuoteIdentifier(tableName);
        string quotedAnchorColumn = QuoteIdentifier(anchorColumnName);

        List<string> columns =
        [
            QuoteIdentifier("name"),
            QuoteIdentifier("ch"),
            QuoteIdentifier("window"),
            quotedAnchorColumn,
        ];

        List<string> values = ["$name", "$ch", "$window", "$anchor"];

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

        string conflictColumns = string.Create(
            CultureInfo.InvariantCulture,
            $"\"name\", \"ch\", \"window\", {quotedAnchorColumn}");

        string sql = conflictMode switch
        {
            SqliteConflictMode.Upsert => string.Concat(
                insertSql,
                " ON CONFLICT(",
                conflictColumns,
                ") DO UPDATE SET ",
                BuildUpsertSetClause(binCount),
                ";"),
            SqliteConflictMode.SkipDuplicate => string.Concat(
                insertSql,
                " ON CONFLICT(",
                conflictColumns,
                ") DO NOTHING;"),
            _ => string.Concat(insertSql, ";"),
        };

        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        _ = command.Parameters.Add("$name", SqliteType.Text);
        _ = command.Parameters.Add("$ch", SqliteType.Integer);
        _ = command.Parameters.Add("$window", SqliteType.Integer);
        _ = command.Parameters.Add("$anchor", SqliteType.Integer);

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

    private sealed class ExistingTableSchema
    {
        public ExistingTableSchema(IReadOnlyList<string> columns, int binCount)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentOutOfRangeException.ThrowIfNegative(binCount);

            Columns = columns;
            BinCount = binCount;
        }

        public IReadOnlyList<string> Columns { get; }

        public int BinCount { get; }

        public bool HasExpectedAnchorOnly(string expectedAnchorColumn)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedAnchorColumn);

            bool hasMs = Columns.Any(column => column.Equals("ms", StringComparison.OrdinalIgnoreCase));
            bool hasSample = Columns.Any(column => column.Equals("sample", StringComparison.OrdinalIgnoreCase));

            if (expectedAnchorColumn.Equals("ms", StringComparison.Ordinal))
            {
                return hasMs && !hasSample;
            }

            if (expectedAnchorColumn.Equals("sample", StringComparison.Ordinal))
            {
                return hasSample && !hasMs;
            }

            return false;
        }
    }
}
#pragma warning restore CA2100
