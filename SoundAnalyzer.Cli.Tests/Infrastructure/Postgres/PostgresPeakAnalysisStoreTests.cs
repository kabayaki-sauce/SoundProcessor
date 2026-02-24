using System.Globalization;
using Npgsql;
using PeakAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Postgres;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Postgres;

#pragma warning disable CA2100
public sealed class PostgresPeakAnalysisStoreTests
{
    [Fact]
    public void InitializeAndWrite_ShouldCreateTableAndInsertRow()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_peak_pg");

        try
        {
            using (PostgresPeakAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       SqliteConflictMode.Error,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new PeakAnalysisPoint("SongA", "Piano", 50, 10, -12.5));
                store.Complete();
            }

            Assert.True(TableExists(connectionOptions, tableName));
            Assert.Equal(1, ReadRowCount(connectionOptions, tableName));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Write_ShouldUpsertByUniqueKey()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_peak_pg");

        try
        {
            PeakAnalysisPoint point = new("SongA", "Piano", 50, 10, -12.0);
            using (PostgresPeakAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       SqliteConflictMode.Upsert,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(point);
                store.Complete();
            }

            using (PostgresPeakAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       SqliteConflictMode.Upsert,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new PeakAnalysisPoint("SongA", "Piano", 50, 10, -3.0));
                store.Complete();
            }

            Assert.Equal(1, ReadRowCount(connectionOptions, tableName));
            Assert.Equal(-3.0, ReadDb(connectionOptions, tableName), precision: 6);
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Write_ShouldSkipDuplicate_WhenSkipDuplicateMode()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_peak_pg");

        try
        {
            PeakAnalysisPoint point = new("SongA", "Piano", 50, 10, -12.0);
            using (PostgresPeakAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       SqliteConflictMode.SkipDuplicate,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(point);
                store.Write(point);
                store.Complete();
            }

            Assert.Equal(1, ReadRowCount(connectionOptions, tableName));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Write_ShouldInsertMultipleRows_WhenBatchRowCountIsOne()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_peak_pg");

        try
        {
            using (PostgresPeakAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       SqliteConflictMode.Error,
                       batchRowCount: 1))
            {
                store.Initialize();
                store.Write(new PeakAnalysisPoint("SongA", "Piano", 50, 10, -12.0));
                store.Write(new PeakAnalysisPoint("SongA", "Piano", 50, 20, -11.0));
                store.Write(new PeakAnalysisPoint("SongA", "Piano", 50, 30, -10.0));
                store.Complete();
            }

            Assert.Equal(3, ReadRowCount(connectionOptions, tableName));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    private static bool TableExists(PostgresConnectionOptions options, string tableName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname = current_schema()
              AND c.relname = @table_name;
            """;
        _ = command.Parameters.AddWithValue("@table_name", tableName);
        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static long ReadRowCount(PostgresConnectionOptions options, string tableName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"SELECT COUNT(1) FROM {QuoteIdentifier(tableName)};");
        object? scalar = command.ExecuteScalar();
        return scalar is long count ? count : 0;
    }

    private static double ReadDb(PostgresConnectionOptions options, string tableName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT \"db\" FROM {QuoteIdentifier(tableName)} WHERE \"name\"='SongA' AND \"stem\"='Piano' AND \"window\"=50 AND \"ms\"=10;");
        object? scalar = command.ExecuteScalar();
        return scalar is double value ? value : 0.0;
    }

    private static void DropTableIfExists(PostgresConnectionOptions options, string tableName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};");
        _ = command.ExecuteNonQuery();
    }

    private static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        string escaped = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture, $"\"{escaped}\"");
    }
}
#pragma warning restore CA2100
