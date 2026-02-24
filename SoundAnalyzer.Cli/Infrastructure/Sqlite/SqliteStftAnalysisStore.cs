using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal sealed class SqliteStftAnalysisStore : IStftAnalysisPointWriter, IDisposable
{
    private const int InsertColumnCount = 8;

    private readonly string dbFilePath;
    private readonly string tableName;
    private readonly string anchorColumnName;
    private readonly SqliteConflictMode conflictMode;
    private readonly int binCount;
    private readonly bool deleteCurrent;
    private readonly SqliteWriteOptions writeOptions;

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;
    private bool completed;
    private bool deferIndexCreation;
    private bool indexesCreated;
    private int effectiveBatchRowCount;

    public SqliteStftAnalysisStore(
        string dbFilePath,
        string tableName,
        string anchorColumnName,
        SqliteConflictMode conflictMode,
        int binCount,
        bool deleteCurrent,
        SqliteWriteOptions? writeOptions = null)
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
        this.writeOptions = writeOptions ?? SqliteWriteOptions.Default;
    }

    public void Initialize()
    {
        if (connection is not null)
        {
            return;
        }

        connection = new SqliteConnection(BuildConnectionString(dbFilePath));
        connection.Open();
        _ = SqliteJournalModeConfigurator.Configure(connection, writeOptions.FastMode);
        effectiveBatchRowCount = SqliteBatchSizeCalculator.ResolveEffectiveBatchRowCount(
            connection,
            writeOptions.BatchRowCount,
            InsertColumnCount);

        transaction = connection.BeginTransaction();
        bool tableExists = TableExists(connection, transaction, tableName);

        if (deleteCurrent)
        {
            DropTableIfExists(connection, transaction, tableName);
            tableExists = false;
        }
        else if (tableExists)
        {
            ExistingTableSchema schema = ReadExistingTableSchema(connection, transaction, tableName);
            if (schema.ContainsLegacyWideColumns)
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, reason=legacy-wide-schema, action=use --delete-current or another table name");
                throw new CliException(CliErrorCode.StftTableSchemaMismatch, detail);
            }

            if (!schema.HasExpectedNormalizedColumns(anchorColumnName))
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, expected-anchor={anchorColumnName}, actual-columns={string.Join(',', schema.Columns)}");
                throw new CliException(CliErrorCode.StftTableSchemaMismatch, detail);
            }

            BinCoverage coverage = ReadBinCoverage(connection, transaction, tableName);
            if (coverage.RowCount > 0
                && (coverage.MaxBinNo != binCount || coverage.DistinctBinCount != binCount))
            {
                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"table={tableName}, expected={binCount}, actual-max={coverage.MaxBinNo}, actual-distinct={coverage.DistinctBinCount}");
                throw new CliException(CliErrorCode.StftTableBinCountMismatch, detail);
            }
        }

        CreateTableIfNeeded(connection, transaction, tableName, anchorColumnName);

        deferIndexCreation = !tableExists && conflictMode == SqliteConflictMode.Error;
        if (!deferIndexCreation)
        {
            CreateIndexesIfNeeded(connection, transaction, tableName, anchorColumnName);
            indexesCreated = true;
        }
    }

    public void Write(StftAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        if (point.Bins.Count != binCount)
        {
            throw new ArgumentException("Point bin-count does not match configured bin-count.", nameof(point));
        }

        SqliteConnection currentConnection = connection
            ?? throw new InvalidOperationException("SqliteStftAnalysisStore is not initialized.");
        SqliteTransaction currentTransaction = transaction
            ?? throw new InvalidOperationException("SqliteStftAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int remaining = binCount;
        int binOffset = 0;
        while (remaining > 0)
        {
            int rowCount = Math.Min(remaining, effectiveBatchRowCount);
            int startBinNo = binOffset + 1;

            using SqliteCommand command = BuildInsertCommand(
                currentConnection,
                currentTransaction,
                tableName,
                anchorColumnName,
                conflictMode,
                point,
                nowMs,
                startBinNo,
                rowCount);
            _ = command.ExecuteNonQuery();

            binOffset += rowCount;
            remaining -= rowCount;
        }
    }

    public void Complete()
    {
        SqliteTransaction? currentTransaction = transaction;
        if (currentTransaction is null)
        {
            return;
        }

        if (deferIndexCreation && !indexesCreated)
        {
            SqliteConnection currentConnection = connection
                ?? throw new InvalidOperationException("SqliteStftAnalysisStore is not initialized.");
            CreateIndexesIfNeeded(currentConnection, currentTransaction, tableName, anchorColumnName);
            indexesCreated = true;
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
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
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
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return new ExistingTableSchema(columns);
    }

    private static BinCoverage ReadBinCoverage(
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
            $"SELECT COALESCE(MAX(\"bin_no\"), 0), COUNT(DISTINCT \"bin_no\"), COUNT(1) FROM {QuoteIdentifier(tableName)};");

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return BinCoverage.Empty;
        }

        int maxBinNo = checked((int)reader.GetInt64(0));
        int distinctBinCount = checked((int)reader.GetInt64(1));
        long rowCount = reader.GetInt64(2);
        return new BinCoverage(maxBinNo, distinctBinCount, rowCount);
    }

    private static void CreateTableIfNeeded(
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
        string quotedAnchorColumn = QuoteIdentifier(anchorColumnName);

        string sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            CREATE TABLE IF NOT EXISTS {quotedTable} (
              "idx" INTEGER PRIMARY KEY AUTOINCREMENT,
              "name" TEXT NOT NULL,
              "ch" INTEGER NOT NULL,
              "window" INTEGER NOT NULL,
              {quotedAnchorColumn} INTEGER NOT NULL,
              "bin_no" INTEGER NOT NULL,
              "db" REAL NOT NULL,
              "create_at" INTEGER NOT NULL,
              "modified_at" INTEGER NOT NULL
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
            BuildUniqueIndexStatement(safePrefix, anchorSuffix, quotedTable, anchorColumnName),
            BuildIndexStatement(safePrefix, "name", quotedTable, "name"),
            BuildIndexStatement(safePrefix, "name_ch", quotedTable, "name", "ch"),
            BuildIndexStatement(safePrefix, string.Create(CultureInfo.InvariantCulture, $"name_{anchorSuffix}"), quotedTable, "name", anchorColumnName),
            BuildIndexStatement(safePrefix, string.Create(CultureInfo.InvariantCulture, $"name_ch_{anchorSuffix}"), quotedTable, "name", "ch", anchorColumnName),
            BuildIndexStatement(safePrefix, string.Create(CultureInfo.InvariantCulture, $"point_{anchorSuffix}"), quotedTable, "name", "ch", "window", anchorColumnName),
        };

        for (int i = 0; i < statements.Length; i++)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statements[i];
            _ = command.ExecuteNonQuery();
        }
    }

    private static string BuildUniqueIndexStatement(
        string prefix,
        string anchorSuffix,
        string quotedTable,
        string anchorColumnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorSuffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(quotedTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);

        string indexName = QuoteIdentifier(
            string.Create(
                CultureInfo.InvariantCulture,
                $"UX_{prefix}_name_ch_window_{anchorSuffix}_bin_no"));
        string joinedColumns = string.Join(
            ", ",
            QuoteIdentifier("name"),
            QuoteIdentifier("ch"),
            QuoteIdentifier("window"),
            QuoteIdentifier(anchorColumnName),
            QuoteIdentifier("bin_no"));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"CREATE UNIQUE INDEX IF NOT EXISTS {indexName} ON {quotedTable} ({joinedColumns});");
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
        StftAnalysisPoint point,
        long nowMs,
        int startBinNo,
        int rowCount)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startBinNo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);

        string quotedTable = QuoteIdentifier(tableName);
        string quotedAnchorColumn = QuoteIdentifier(anchorColumnName);

        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;

        StringBuilder valuesBuilder = new();
        for (int i = 0; i < rowCount; i++)
        {
            if (i > 0)
            {
                _ = valuesBuilder.Append(", ");
            }

            int binNo = startBinNo + i;
            _ = valuesBuilder.Append('(');
            _ = valuesBuilder.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"$name{i}, $ch{i}, $window{i}, $anchor{i}, $binNo{i}, $db{i}, $createAt{i}, $modifiedAt{i}"));
            _ = valuesBuilder.Append(')');

            _ = command.Parameters.AddWithValue($"$name{i}", point.Name);
            _ = command.Parameters.AddWithValue($"$ch{i}", point.Channel);
            _ = command.Parameters.AddWithValue($"$window{i}", point.Window);
            _ = command.Parameters.AddWithValue($"$anchor{i}", point.Anchor);
            _ = command.Parameters.AddWithValue($"$binNo{i}", binNo);
            _ = command.Parameters.AddWithValue($"$db{i}", point.Bins[binNo - 1]);
            _ = command.Parameters.AddWithValue($"$createAt{i}", nowMs);
            _ = command.Parameters.AddWithValue($"$modifiedAt{i}", nowMs);
        }

        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO {quotedTable} (\"name\", \"ch\", \"window\", {quotedAnchorColumn}, \"bin_no\", \"db\", \"create_at\", \"modified_at\") VALUES ");

        string conflictColumns = string.Create(
            CultureInfo.InvariantCulture,
            $"\"name\", \"ch\", \"window\", {quotedAnchorColumn}, \"bin_no\"");

        string conflictClause = conflictMode switch
        {
            SqliteConflictMode.Upsert => string.Concat(
                " ON CONFLICT(",
                conflictColumns,
                ") DO UPDATE SET \"db\" = excluded.\"db\", \"modified_at\" = excluded.\"modified_at\""),
            SqliteConflictMode.SkipDuplicate => string.Concat(
                " ON CONFLICT(",
                conflictColumns,
                ") DO NOTHING"),
            _ => string.Empty,
        };

        command.CommandText = string.Concat(prefix, valuesBuilder.ToString(), conflictClause, ";");
        command.Prepare();
        return command;
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
        public ExistingTableSchema(IReadOnlyList<string> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);
            Columns = columns;
        }

        public IReadOnlyList<string> Columns { get; }

        public bool ContainsLegacyWideColumns => Columns.Any(IsLegacyWideBinColumn);

        public bool HasExpectedNormalizedColumns(string expectedAnchorColumn)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedAnchorColumn);

            bool hasMs = HasColumn("ms");
            bool hasSample = HasColumn("sample");
            bool hasExpectedAnchor = expectedAnchorColumn.Equals("ms", StringComparison.Ordinal)
                ? hasMs && !hasSample
                : hasSample && !hasMs;

            return hasExpectedAnchor
                && HasColumn("idx")
                && HasColumn("name")
                && HasColumn("ch")
                && HasColumn("window")
                && HasColumn("bin_no")
                && HasColumn("db")
                && HasColumn("create_at")
                && HasColumn("modified_at")
                && !ContainsLegacyWideColumns;
        }

        private static bool IsLegacyWideBinColumn(string columnName)
        {
            if (!columnName.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (columnName.Equals("bin_no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ReadOnlySpan<char> suffix = columnName.AsSpan(3);
            if (suffix.Length != 3)
            {
                return false;
            }

            for (int i = 0; i < suffix.Length; i++)
            {
                if (!char.IsAsciiDigit(suffix[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasColumn(string columnName)
        {
            return Columns.Any(column => column.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private readonly record struct BinCoverage(int MaxBinNo, int DistinctBinCount, long RowCount)
    {
        public static BinCoverage Empty { get; } = new(0, 0, 0);
    }
}
#pragma warning restore CA2100
