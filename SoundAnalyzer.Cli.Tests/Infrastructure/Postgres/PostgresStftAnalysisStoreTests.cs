using System.Globalization;
using Npgsql;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.Postgres;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Postgres;

#pragma warning disable CA2100
public sealed class PostgresStftAnalysisStoreTests
{
    [Fact]
    public void InitializeAndWrite_ShouldCreateTableAndInsertRows()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");

        try
        {
            using (PostgresStftAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -12.0)));
                store.Complete();
            }

            Assert.Equal(4, ReadRowCount(connectionOptions, tableName));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Write_ShouldClampBatchRowCount_WhenConfiguredBatchIsTooLarge()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");
        const int binCount = 9_000;

        try
        {
            using (PostgresStftAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: binCount,
                       deleteCurrent: false,
                       batchRowCount: 100_000))
            {
                store.Initialize();
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(binCount, -12.0)));
                store.Complete();
            }

            Assert.Equal(binCount, ReadRowCount(connectionOptions, tableName));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Write_ShouldUpsertAndSkipDuplicate()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");

        try
        {
            using (PostgresStftAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -12.0)));
                store.Complete();
            }

            using (PostgresStftAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -6.0)));
                store.Complete();
            }

            Assert.Equal(4, ReadRowCount(connectionOptions, tableName));
            Assert.Equal(-6.0, ReadBinDb(connectionOptions, tableName, 1), precision: 6);

            using (PostgresStftAnalysisStore store = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.SkipDuplicate,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                store.Initialize();
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -2.0)));
                store.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -2.0)));
                store.Complete();
            }

            Assert.Equal(4, ReadRowCount(connectionOptions, tableName));
            Assert.Equal(-6.0, ReadBinDb(connectionOptions, tableName, 1), precision: 6);
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Initialize_ShouldFail_WhenExistingBinCountDiffers()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");

        try
        {
            using (PostgresStftAnalysisStore first = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                first.Initialize();
                first.Write(new StftAnalysisPoint("SongA", 0, 50, 10, CreateBins(4, -12.0)));
                first.Complete();
            }

            using PostgresStftAnalysisStore second = new(
                connectionOptions,
                sshOptions: null,
                tableName,
                anchorColumnName: "ms",
                conflictMode: SqliteConflictMode.Error,
                binCount: 8,
                deleteCurrent: false,
                batchRowCount: 512);

            CliException exception = Assert.Throws<CliException>(() => second.Initialize());
            Assert.Equal(CliErrorCode.StftTableBinCountMismatch, exception.ErrorCode);
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Initialize_ShouldFail_WhenSchemaAnchorIsDifferent()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");

        try
        {
            using (PostgresStftAnalysisStore first = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                first.Initialize();
                first.Complete();
            }

            using PostgresStftAnalysisStore second = new(
                connectionOptions,
                sshOptions: null,
                tableName,
                anchorColumnName: "sample",
                conflictMode: SqliteConflictMode.Error,
                binCount: 4,
                deleteCurrent: false,
                batchRowCount: 512);

            CliException exception = Assert.Throws<CliException>(() => second.Initialize());
            Assert.Equal(CliErrorCode.StftTableSchemaMismatch, exception.ErrorCode);
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    [Fact]
    public void Initialize_ShouldRecreateTable_WhenDeleteCurrentIsEnabled()
    {
        PostgresConnectionOptions? connectionOptions = PostgresTestEnvironment.GetConnectionOptionsOrNull();
        if (connectionOptions is null)
        {
            return;
        }
        string tableName = PostgresTestEnvironment.CreateRandomTableName("t_stft_pg");

        try
        {
            using (PostgresStftAnalysisStore first = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "ms",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: 4,
                       deleteCurrent: false,
                       batchRowCount: 512))
            {
                first.Initialize();
                first.Complete();
            }

            using (PostgresStftAnalysisStore second = new(
                       connectionOptions,
                       sshOptions: null,
                       tableName,
                       anchorColumnName: "sample",
                       conflictMode: SqliteConflictMode.Error,
                       binCount: 6,
                       deleteCurrent: true,
                       batchRowCount: 512))
            {
                second.Initialize();
                second.Complete();
            }

            Assert.True(ColumnExists(connectionOptions, tableName, "sample"));
            Assert.False(ColumnExists(connectionOptions, tableName, "ms"));
        }
        finally
        {
            DropTableIfExists(connectionOptions, tableName);
        }
    }

    private static double[] CreateBins(int count, double value)
    {
        double[] bins = new double[count];
        for (int i = 0; i < bins.Length; i++)
        {
            bins[i] = value;
        }

        return bins;
    }

    private static long ReadRowCount(PostgresConnectionOptions options, string tableName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"SELECT COUNT(1) FROM {QuoteIdentifier(tableName)};");
        object? scalar = command.ExecuteScalar();
        return scalar is long count ? count : 0;
    }

    private static double ReadBinDb(PostgresConnectionOptions options, string tableName, int binNo)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT \"db\" FROM {QuoteIdentifier(tableName)} WHERE \"name\"='SongA' AND \"ch\"=0 AND \"window\"=50 AND \"ms\"=10 AND \"bin_no\"=@bin_no;");
        _ = command.Parameters.AddWithValue("@bin_no", binNo);
        object? scalar = command.ExecuteScalar();
        return scalar is double value ? value : 0.0;
    }

    private static bool ColumnExists(PostgresConnectionOptions options, string tableName, string columnName)
    {
        using NpgsqlConnection connection = PostgresTestEnvironment.OpenConnection(options);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @table_name
              AND lower(column_name) = lower(@column_name);
            """;
        _ = command.Parameters.AddWithValue("@table_name", tableName);
        _ = command.Parameters.AddWithValue("@column_name", columnName);
        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
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
