using System.Globalization;
using Microsoft.Data.Sqlite;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Domain.Models;

namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

#pragma warning disable CA2100
internal sealed class SqlitePeakAnalysisStore : IPeakAnalysisPointWriter, IDisposable
{
    private readonly string dbFilePath;
    private readonly string tableName;
    private readonly SqliteConflictMode conflictMode;

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;
    private SqliteCommand? insertCommand;
    private bool completed;

    public SqlitePeakAnalysisStore(string dbFilePath, string tableName, SqliteConflictMode conflictMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        this.dbFilePath = dbFilePath;
        this.tableName = tableName;
        this.conflictMode = conflictMode;
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
        CreateTableIfNeeded(connection, transaction, tableName);
        CreateIndexesIfNeeded(connection, transaction, tableName);

        insertCommand = BuildInsertCommand(connection, transaction, tableName, conflictMode);
    }

    public void Write(PeakAnalysisPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        SqliteCommand command = insertCommand
            ?? throw new InvalidOperationException("SqlitePeakAnalysisStore is not initialized.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        command.Parameters["$name"].Value = point.Name;
        command.Parameters["$stem"].Value = point.Stem;
        command.Parameters["$window"].Value = point.WindowMs;
        command.Parameters["$ms"].Value = point.Ms;
        command.Parameters["$db"].Value = point.Db;
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
              "modified_at" INTEGER NOT NULL,
              UNIQUE("name", "stem", "window", "ms")
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
        SqliteConflictMode conflictMode)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        string quotedTable = QuoteIdentifier(tableName);

        string sql = conflictMode switch
        {
            SqliteConflictMode.Upsert => string.Create(
                CultureInfo.InvariantCulture,
                $"""
                INSERT INTO {quotedTable} ("name", "stem", "window", "ms", "db", "create_at", "modified_at")
                VALUES ($name, $stem, $window, $ms, $db, $createAt, $modifiedAt)
                ON CONFLICT("name", "stem", "window", "ms") DO UPDATE SET
                  "db" = excluded."db",
                  "modified_at" = excluded."modified_at";
                """),
            SqliteConflictMode.SkipDuplicate => string.Create(
                CultureInfo.InvariantCulture,
                $"""
                INSERT INTO {quotedTable} ("name", "stem", "window", "ms", "db", "create_at", "modified_at")
                VALUES ($name, $stem, $window, $ms, $db, $createAt, $modifiedAt)
                ON CONFLICT("name", "stem", "window", "ms") DO NOTHING;
                """),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"""
                INSERT INTO {quotedTable} ("name", "stem", "window", "ms", "db", "create_at", "modified_at")
                VALUES ($name, $stem, $window, $ms, $db, $createAt, $modifiedAt);
                """),
        };

        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        _ = command.Parameters.Add("$name", SqliteType.Text);
        _ = command.Parameters.Add("$stem", SqliteType.Text);
        _ = command.Parameters.Add("$window", SqliteType.Integer);
        _ = command.Parameters.Add("$ms", SqliteType.Integer);
        _ = command.Parameters.Add("$db", SqliteType.Real);
        _ = command.Parameters.Add("$createAt", SqliteType.Integer);
        _ = command.Parameters.Add("$modifiedAt", SqliteType.Integer);
        command.Prepare();

        return command;
    }

    private static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        string escaped = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture, $"\"{escaped}\"");
    }
}
#pragma warning restore CA2100
