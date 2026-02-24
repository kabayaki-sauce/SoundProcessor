using System.Globalization;
using System.Text;
using Npgsql;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using STFTAnalyzer.Core.Domain.Models;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

#pragma warning disable CA2100
internal sealed class PostgresStftAnalysisStore : IStftAnalysisStore
{
    private const int InsertColumnCount = 8;
    private const int DefaultBatchRowCount = 512;
    private const int MaxParameterCount = 65_535;

    private readonly PostgresConnectionOptions connectionOptions;
    private readonly PostgresSshOptions? sshOptions;
    private readonly string tableName;
    private readonly string anchorColumnName;
    private readonly SqliteConflictMode conflictMode;
    private readonly int binCount;
    private readonly bool deleteCurrent;

    private PostgresSession? session;
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private bool completed;
    private bool deferIndexCreation;
    private bool indexesCreated;
    private int effectiveBatchRowCount;

    public PostgresStftAnalysisStore(
        PostgresConnectionOptions connectionOptions,
        PostgresSshOptions? sshOptions,
        string tableName,
        string anchorColumnName,
        SqliteConflictMode conflictMode,
        int binCount,
        bool deleteCurrent)
    {
        this.connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
        this.sshOptions = sshOptions;
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        string normalizedAnchor = anchorColumnName.Trim().ToLowerInvariant();
        if (!normalizedAnchor.Equals("ms", StringComparison.Ordinal)
            && !normalizedAnchor.Equals("sample", StringComparison.Ordinal))
        {
            throw new ArgumentException("Anchor column must be 'ms' or 'sample'.", nameof(anchorColumnName));
        }

        this.tableName = tableName;
        this.anchorColumnName = normalizedAnchor;
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

        session = PostgresConnectionFactory.OpenSession(connectionOptions, sshOptions);
        connection = session.Connection;
        transaction = connection.BeginTransaction();
        effectiveBatchRowCount = ResolveEffectiveBatchRowCount(DefaultBatchRowCount, InsertColumnCount);

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

        NpgsqlConnection currentConnection = connection
            ?? throw new InvalidOperationException("PostgresStftAnalysisStore is not initialized.");
        NpgsqlTransaction currentTransaction = transaction
            ?? throw new InvalidOperationException("PostgresStftAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int remaining = binCount;
        int binOffset = 0;
        while (remaining > 0)
        {
            int rowCount = Math.Min(remaining, effectiveBatchRowCount);
            int startBinNo = binOffset + 1;

            using NpgsqlCommand command = BuildInsertCommand(
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
        NpgsqlTransaction? currentTransaction = transaction;
        if (currentTransaction is null)
        {
            return;
        }

        if (deferIndexCreation && !indexesCreated)
        {
            NpgsqlConnection currentConnection = connection
                ?? throw new InvalidOperationException("PostgresStftAnalysisStore is not initialized.");
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
            session?.Dispose();
        }
    }

    private static int ResolveEffectiveBatchRowCount(int requestedBatchRowCount, int columnsPerRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedBatchRowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnsPerRow);

        int maxRows = Math.Max(1, MaxParameterCount / columnsPerRow);
        return Math.Min(requestedBatchRowCount, maxRows);
    }

    private static bool TableExists(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname = current_schema()
              AND c.relname = @name;
            """;
        _ = command.Parameters.AddWithValue("@name", tableName);

        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static void DropTableIfExists(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};");
        _ = command.ExecuteNonQuery();
    }

    private static ExistingTableSchema ReadExistingTableSchema(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @name;
            """;
        _ = command.Parameters.AddWithValue("@name", tableName);

        List<string> columns = new();
        using NpgsqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return new ExistingTableSchema(columns);
    }

    private static BinCoverage ReadBinCoverage(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT COALESCE(MAX(\"bin_no\"), 0), COUNT(DISTINCT \"bin_no\"), COUNT(1) FROM {QuoteIdentifier(tableName)};");

        using NpgsqlDataReader reader = command.ExecuteReader();
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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        string anchorColumnName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);

        string quotedTable = QuoteIdentifier(tableName);
        string quotedAnchor = QuoteIdentifier(anchorColumnName);
        string sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            CREATE TABLE IF NOT EXISTS {quotedTable} (
              "idx" BIGSERIAL PRIMARY KEY,
              "name" TEXT NOT NULL,
              "ch" INTEGER NOT NULL,
              "window" BIGINT NOT NULL,
              {quotedAnchor} BIGINT NOT NULL,
              "bin_no" INTEGER NOT NULL,
              "db" DOUBLE PRECISION NOT NULL,
              "create_at" BIGINT NOT NULL,
              "modified_at" BIGINT NOT NULL
            );
            """);

        using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static void CreateIndexesIfNeeded(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
            using NpgsqlCommand command = connection.CreateCommand();
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
        string joinedColumns = string.Join(", ", columns.Select(QuoteIdentifier));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"CREATE INDEX IF NOT EXISTS {indexName} ON {quotedTable} ({joinedColumns});");
    }

    private static NpgsqlCommand BuildInsertCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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

        NpgsqlCommand command = connection.CreateCommand();
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
                    $"@name{i}, @ch{i}, @window{i}, @anchor{i}, @binNo{i}, @db{i}, @createAt{i}, @modifiedAt{i}"));
            _ = valuesBuilder.Append(')');

            _ = command.Parameters.AddWithValue($"@name{i}", point.Name);
            _ = command.Parameters.AddWithValue($"@ch{i}", point.Channel);
            _ = command.Parameters.AddWithValue($"@window{i}", point.Window);
            _ = command.Parameters.AddWithValue($"@anchor{i}", point.Anchor);
            _ = command.Parameters.AddWithValue($"@binNo{i}", binNo);
            _ = command.Parameters.AddWithValue($"@db{i}", point.Bins[binNo - 1]);
            _ = command.Parameters.AddWithValue($"@createAt{i}", nowMs);
            _ = command.Parameters.AddWithValue($"@modifiedAt{i}", nowMs);
        }

        string quotedTable = QuoteIdentifier(tableName);
        string quotedAnchor = QuoteIdentifier(anchorColumnName);
        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO {quotedTable} (\"name\", \"ch\", \"window\", {quotedAnchor}, \"bin_no\", \"db\", \"create_at\", \"modified_at\") VALUES ");
        string conflictColumns = string.Create(
            CultureInfo.InvariantCulture,
            $"\"name\", \"ch\", \"window\", {quotedAnchor}, \"bin_no\"");
        string conflictClause = conflictMode switch
        {
            SqliteConflictMode.Upsert => string.Concat(
                " ON CONFLICT(",
                conflictColumns,
                ") DO UPDATE SET \"db\" = EXCLUDED.\"db\", \"modified_at\" = EXCLUDED.\"modified_at\""),
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
