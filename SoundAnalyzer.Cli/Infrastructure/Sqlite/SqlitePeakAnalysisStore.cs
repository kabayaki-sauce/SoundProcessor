using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Domain.Models;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal sealed class SqlitePeakAnalysisStore : IPeakAnalysisPointWriter, IDisposable
{
    private const int InsertColumnCount = 7;

    private readonly string dbFilePath;
    private readonly string tableName;
    private readonly SqliteConflictMode conflictMode;
    private readonly SqliteWriteOptions writeOptions;
    private readonly List<PeakInsertRow> pendingRows = new();

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;
    private bool completed;
    private bool deferIndexCreation;
    private bool indexesCreated;
    private int effectiveBatchRowCount;

    public SqlitePeakAnalysisStore(
        string dbFilePath,
        string tableName,
        SqliteConflictMode conflictMode,
        SqliteWriteOptions? writeOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        this.dbFilePath = dbFilePath;
        this.tableName = tableName;
        this.conflictMode = conflictMode;
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

        bool tableExists = TableExists(connection, tableName);

        transaction = connection.BeginTransaction();
        CreateTableIfNeeded(connection, transaction, tableName);

        deferIndexCreation = !tableExists && conflictMode == SqliteConflictMode.Error;
        if (!deferIndexCreation)
        {
            CreateIndexesIfNeeded(connection, transaction, tableName);
            indexesCreated = true;
        }
    }

    public void Write(PeakAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        _ = connection ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");
        _ = transaction ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pendingRows.Add(new PeakInsertRow(
            point.Name,
            point.Stem,
            point.WindowMs,
            point.Ms,
            point.Db,
            nowMs,
            nowMs));

        if (pendingRows.Count >= effectiveBatchRowCount)
        {
            FlushPendingRows();
        }
    }

    public void Complete()
    {
        SqliteTransaction? currentTransaction = transaction;
        if (currentTransaction is null)
        {
            return;
        }

        FlushPendingRows();

        if (deferIndexCreation && !indexesCreated)
        {
            SqliteConnection currentConnection = connection
                ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");
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
            connection?.Dispose();
        }
    }

    private void FlushPendingRows()
    {
        if (pendingRows.Count == 0)
        {
            return;
        }

        SqliteConnection currentConnection = connection
            ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");
        SqliteTransaction currentTransaction = transaction
            ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");

        using SqliteCommand command = BuildInsertCommand(
            currentConnection,
            currentTransaction,
            tableName,
            conflictMode,
            pendingRows);
        _ = command.ExecuteNonQuery();
        pendingRows.Clear();
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

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        _ = command.Parameters.AddWithValue("$name", tableName);

        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static void CreateTableIfNeeded(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        string quotedTable = QuoteIdentifier(tableName);
        string sql = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            CREATE TABLE IF NOT EXISTS {quotedTable} (
              "idx" INTEGER PRIMARY KEY AUTOINCREMENT,
              "name" TEXT NOT NULL,
              "stem" TEXT NOT NULL,
              "window" INTEGER NOT NULL,
              "ms" INTEGER NOT NULL,
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

    private static void CreateIndexesIfNeeded(SqliteConnection connection, SqliteTransaction transaction, string tableName)
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
            using SqliteCommand command = connection.CreateCommand();
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
        string joinedColumns = string.Join(", ", columns.Select(column => QuoteIdentifier(column)));

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

        string quotedTable = QuoteIdentifier(tableName);
        SqliteCommand command = connection.CreateCommand();
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
                    $"$name{i}, $stem{i}, $window{i}, $ms{i}, $db{i}, $createAt{i}, $modifiedAt{i}"));
            _ = valuesBuilder.Append(')');

            PeakInsertRow row = rows[i];
            _ = command.Parameters.AddWithValue($"$name{i}", row.Name);
            _ = command.Parameters.AddWithValue($"$stem{i}", row.Stem);
            _ = command.Parameters.AddWithValue($"$window{i}", row.WindowMs);
            _ = command.Parameters.AddWithValue($"$ms{i}", row.Ms);
            _ = command.Parameters.AddWithValue($"$db{i}", row.Db);
            _ = command.Parameters.AddWithValue($"$createAt{i}", row.CreateAt);
            _ = command.Parameters.AddWithValue($"$modifiedAt{i}", row.ModifiedAt);
        }

        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO {quotedTable} (\"name\", \"stem\", \"window\", \"ms\", \"db\", \"create_at\", \"modified_at\") VALUES ");

        string conflictClause = conflictMode switch
        {
            SqliteConflictMode.Upsert =>
                " ON CONFLICT(\"name\", \"stem\", \"window\", \"ms\") DO UPDATE SET \"db\" = excluded.\"db\", \"modified_at\" = excluded.\"modified_at\"",
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
