using System.Globalization;
using System.Text;
using Npgsql;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

#pragma warning disable CA2100
internal sealed class PostgresPeakAnalysisStore : IPeakAnalysisStore
{
    private const int DefaultBatchRowCount = 512;
    private readonly PostgresConnectionOptions connectionOptions;
    private readonly PostgresSshOptions? sshOptions;
    private readonly string tableName;
    private readonly SqliteConflictMode conflictMode;
    private readonly List<PeakInsertRow> pendingRows = new();

    private PostgresSession? session;
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;
    private bool completed;
    private bool deferIndexCreation;
    private bool indexesCreated;

    public PostgresPeakAnalysisStore(
        PostgresConnectionOptions connectionOptions,
        PostgresSshOptions? sshOptions,
        string tableName,
        SqliteConflictMode conflictMode)
    {
        this.connectionOptions = connectionOptions ?? throw new ArgumentNullException(nameof(connectionOptions));
        this.sshOptions = sshOptions;
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        this.tableName = tableName;
        this.conflictMode = conflictMode;
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

        bool tableExists = TableExists(connection, transaction, tableName);
        CreateTableIfNeeded(connection, transaction, tableName);

        deferIndexCreation = !tableExists && conflictMode == SqliteConflictMode.Error;
        if (!deferIndexCreation)
        {
            CreateIndexesIfNeeded(connection, transaction, tableName);
            indexesCreated = true;
        }
    }

    public void Write(PeakAnalyzer.Core.Domain.Models.PeakAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        EnsureInitialized();

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pendingRows.Add(new PeakInsertRow(
            point.Name,
            point.Stem,
            point.WindowMs,
            point.Ms,
            point.Db,
            nowMs,
            nowMs));

        if (pendingRows.Count >= DefaultBatchRowCount)
        {
            FlushPendingRows();
        }
    }

    public void Complete()
    {
        NpgsqlTransaction? currentTransaction = transaction;
        if (currentTransaction is null)
        {
            return;
        }

        FlushPendingRows();

        if (deferIndexCreation && !indexesCreated)
        {
            NpgsqlConnection currentConnection = connection
                ?? throw new InvalidOperationException("PostgresPeakAnalysisStore is not initialized.");
            CreateIndexesIfNeeded(currentConnection, currentTransaction, tableName);
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

    private void EnsureInitialized()
    {
        _ = connection ?? throw new InvalidOperationException("PostgresPeakAnalysisStore is not initialized.");
        _ = transaction ?? throw new InvalidOperationException("PostgresPeakAnalysisStore is not initialized.");
    }

    private void FlushPendingRows()
    {
        if (pendingRows.Count == 0)
        {
            return;
        }

        NpgsqlConnection currentConnection = connection
            ?? throw new InvalidOperationException("PostgresPeakAnalysisStore is not initialized.");
        NpgsqlTransaction currentTransaction = transaction
            ?? throw new InvalidOperationException("PostgresPeakAnalysisStore is not initialized.");

        using NpgsqlCommand command = BuildInsertCommand(
            currentConnection,
            currentTransaction,
            tableName,
            conflictMode,
            pendingRows);
        _ = command.ExecuteNonQuery();
        pendingRows.Clear();
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

    private static void CreateTableIfNeeded(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        string quotedTable = QuoteIdentifier(tableName);
        string sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            CREATE TABLE IF NOT EXISTS {quotedTable} (
              "idx" BIGSERIAL PRIMARY KEY,
              "name" TEXT NOT NULL,
              "stem" TEXT NOT NULL,
              "window" BIGINT NOT NULL,
              "ms" BIGINT NOT NULL,
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

    private static void CreateIndexesIfNeeded(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        string quotedTable = QuoteIdentifier(tableName);
        string safePrefix = tableName.Replace("-", "_", StringComparison.Ordinal);

        string[] statements =
        {
            BuildUniqueIndexStatement(safePrefix, quotedTable, "name", "stem", "window", "ms"),
            BuildIndexStatement(safePrefix, "name", quotedTable, "name"),
            BuildIndexStatement(safePrefix, "name_stem", quotedTable, "name", "stem"),
            BuildIndexStatement(safePrefix, "name_ms", quotedTable, "name", "ms"),
            BuildIndexStatement(safePrefix, "name_stem_ms", quotedTable, "name", "stem", "ms"),
        };

        for (int i = 0; i < statements.Length; i++)
        {
            using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statements[i];
            _ = command.ExecuteNonQuery();
        }
    }

    private static string BuildUniqueIndexStatement(string prefix, string quotedTable, params string[] columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(quotedTable);
        ArgumentNullException.ThrowIfNull(columns);

        string indexName = QuoteIdentifier(string.Create(CultureInfo.InvariantCulture, $"UX_{prefix}_name_stem_window_ms"));
        string joinedColumns = string.Join(", ", columns.Select(QuoteIdentifier));
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
        SqliteConflictMode conflictMode,
        IReadOnlyList<PeakInsertRow> rows)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            throw new ArgumentException("At least one row is required.", nameof(rows));
        }

        NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;

        StringBuilder valuesBuilder = new();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                _ = valuesBuilder.Append(", ");
            }

            _ = valuesBuilder.Append('(');
            _ = valuesBuilder.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"@name{i}, @stem{i}, @window{i}, @ms{i}, @db{i}, @createAt{i}, @modifiedAt{i}"));
            _ = valuesBuilder.Append(')');

            PeakInsertRow row = rows[i];
            _ = command.Parameters.AddWithValue($"@name{i}", row.Name);
            _ = command.Parameters.AddWithValue($"@stem{i}", row.Stem);
            _ = command.Parameters.AddWithValue($"@window{i}", row.WindowMs);
            _ = command.Parameters.AddWithValue($"@ms{i}", row.Ms);
            _ = command.Parameters.AddWithValue($"@db{i}", row.Db);
            _ = command.Parameters.AddWithValue($"@createAt{i}", row.CreateAt);
            _ = command.Parameters.AddWithValue($"@modifiedAt{i}", row.ModifiedAt);
        }

        string quotedTable = QuoteIdentifier(tableName);
        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO {quotedTable} (\"name\", \"stem\", \"window\", \"ms\", \"db\", \"create_at\", \"modified_at\") VALUES ");
        string conflictClause = conflictMode switch
        {
            SqliteConflictMode.Upsert =>
                " ON CONFLICT(\"name\", \"stem\", \"window\", \"ms\") DO UPDATE SET \"db\" = EXCLUDED.\"db\", \"modified_at\" = EXCLUDED.\"modified_at\"",
            SqliteConflictMode.SkipDuplicate =>
                " ON CONFLICT(\"name\", \"stem\", \"window\", \"ms\") DO NOTHING",
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

    private readonly record struct PeakInsertRow(
        string Name,
        string Stem,
        long WindowMs,
        long Ms,
        double Db,
        long CreateAt,
        long ModifiedAt);
}
#pragma warning restore CA2100
