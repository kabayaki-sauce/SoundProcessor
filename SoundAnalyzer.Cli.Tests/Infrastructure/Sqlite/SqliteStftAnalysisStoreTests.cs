using Microsoft.Data.Sqlite;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Sqlite;

public sealed class SqliteStftAnalysisStoreTests
{
    [Fact]
    public void Initialize_ShouldCreateTableWithMsAnchor_WhenAnchorColumnIsMs()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Complete();
            }

            Assert.True(File.Exists(dbFilePath));
            Assert.True(TableExists(dbFilePath, "T_STFTAnalysis"));
            Assert.True(ColumnExists(dbFilePath, "T_STFTAnalysis", "ms"));
            Assert.False(ColumnExists(dbFilePath, "T_STFTAnalysis", "sample"));
            Assert.Equal(12, CountBinColumns(dbFilePath));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldCreateTableWithSampleAnchor_WhenAnchorColumnIsSample()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "sample",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Complete();
            }

            Assert.True(ColumnExists(dbFilePath, "T_STFTAnalysis", "sample"));
            Assert.False(ColumnExists(dbFilePath, "T_STFTAnalysis", "ms"));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldFail_WhenExistingTableBinCountDiffers()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore first = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Error,
                       binCount: 8,
                       deleteCurrent: false))
            {
                first.Initialize();
                first.Complete();
            }

            using SqliteStftAnalysisStore second = new(
                dbFilePath,
                "T_STFTAnalysis",
                "ms",
                SqliteConflictMode.Error,
                binCount: 12,
                deleteCurrent: false);

            CliException exception = Assert.Throws<CliException>(() => second.Initialize());
            Assert.Equal(CliErrorCode.StftTableBinCountMismatch, exception.ErrorCode);
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldFail_WhenExistingAnchorColumnDiffers()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore first = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Error,
                       binCount: 4,
                       deleteCurrent: false))
            {
                first.Initialize();
                first.Complete();
            }

            using SqliteStftAnalysisStore second = new(
                dbFilePath,
                "T_STFTAnalysis",
                "sample",
                SqliteConflictMode.Error,
                binCount: 4,
                deleteCurrent: false);

            CliException exception = Assert.Throws<CliException>(() => second.Initialize());
            Assert.Equal(CliErrorCode.StftTableSchemaMismatch, exception.ErrorCode);
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldPreferWalJournalMode()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Complete();
            }

            string mode = ReadJournalMode(dbFilePath);
            Assert.True(IsKnownJournalMode(mode));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldRecreateTable_WhenDeleteCurrentIsEnabled()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteStftAnalysisStore first = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Error,
                       binCount: 8,
                       deleteCurrent: false))
            {
                first.Initialize();
                first.Complete();
            }

            using (SqliteStftAnalysisStore second = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "sample",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: true))
            {
                second.Initialize();
                second.Complete();
            }

            Assert.Equal(12, CountBinColumns(dbFilePath));
            Assert.True(ColumnExists(dbFilePath, "T_STFTAnalysis", "sample"));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Write_ShouldUpsertAndKeepCreateAt_ForMsAnchor()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            StftAnalysisPoint firstPoint = new("Song", 0, 50, 10, CreateBins(4, -10));

            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Write(firstPoint);
                store.Complete();
            }

            (long createAtBefore, long modifiedAtBefore, double bin001Before) = ReadSingleRow(dbFilePath, "ms", 10);
            _ = SpinWait.SpinUntil(
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > modifiedAtBefore,
                TimeSpan.FromMilliseconds(200));

            StftAnalysisPoint updatedPoint = new("Song", 0, 50, 10, CreateBins(4, -3));
            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "ms",
                       SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Write(updatedPoint);
                store.Complete();
            }

            (long createAtAfter, long modifiedAtAfter, double bin001After) = ReadSingleRow(dbFilePath, "ms", 10);

            Assert.Equal(createAtBefore, createAtAfter);
            Assert.True(modifiedAtAfter >= modifiedAtBefore);
            Assert.NotEqual(bin001Before, bin001After);
            Assert.Equal(-3, bin001After, precision: 6);
            Assert.Equal(1, ReadRowCount(dbFilePath));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Write_ShouldSkipDuplicate_WhenSkipDuplicateIsEnabled_ForSampleAnchor()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            StftAnalysisPoint point = new("Song", 0, 2048, 512, CreateBins(4, -10));

            using (SqliteStftAnalysisStore store = new(
                       dbFilePath,
                       "T_STFTAnalysis",
                       "sample",
                       SqliteConflictMode.SkipDuplicate,
                       binCount: 4,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Write(point);
                store.Write(point);
                store.Complete();
            }

            Assert.Equal(1, ReadRowCount(dbFilePath));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    private static double[] CreateBins(int count, double value)
    {
        double[] bins = new double[count];
        for (int i = 0; i < count; i++)
        {
            bins[i] = value;
        }

        return bins;
    }

    private static SqliteConnection OpenConnection(string dbFilePath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = dbFilePath,
            Pooling = false,
        };

        SqliteConnection connection = new(builder.ToString());
        connection.Open();
        return connection;
    }

    private static bool TableExists(string dbFilePath, string tableName)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        _ = command.Parameters.AddWithValue("$name", tableName);
        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static bool ColumnExists(string dbFilePath, string tableName, string columnName)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM pragma_table_info($table_name) WHERE lower(name) = lower($column_name);";
        _ = command.Parameters.AddWithValue("$table_name", tableName);
        _ = command.Parameters.AddWithValue("$column_name", columnName);

        object? scalar = command.ExecuteScalar();
        return scalar is long count && count > 0;
    }

    private static int CountBinColumns(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"T_STFTAnalysis\");";

        int count = 0;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(1);
            if (name.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static (long CreateAt, long ModifiedAt, double Bin001) ReadSingleRow(
        string dbFilePath,
        string anchorColumnName,
        long anchorValue)
    {
        return anchorColumnName.Equals("sample", StringComparison.OrdinalIgnoreCase)
            ? ReadSingleRowBySampleAnchor(dbFilePath, anchorValue)
            : ReadSingleRowByMsAnchor(dbFilePath, anchorValue);
    }

    private static (long CreateAt, long ModifiedAt, double Bin001) ReadSingleRowByMsAnchor(
        string dbFilePath,
        long anchorValue)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT create_at, modified_at, bin001 FROM \"T_STFTAnalysis\" WHERE name = 'Song' AND ch = 0 AND window = 50 AND ms = $anchor;";
        _ = command.Parameters.AddWithValue("$anchor", anchorValue);

        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());

        long createAt = reader.GetInt64(0);
        long modifiedAt = reader.GetInt64(1);
        double bin001 = reader.GetDouble(2);
        return (createAt, modifiedAt, bin001);
    }

    private static (long CreateAt, long ModifiedAt, double Bin001) ReadSingleRowBySampleAnchor(
        string dbFilePath,
        long anchorValue)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT create_at, modified_at, bin001 FROM \"T_STFTAnalysis\" WHERE name = 'Song' AND ch = 0 AND window = 50 AND sample = $anchor;";
        _ = command.Parameters.AddWithValue("$anchor", anchorValue);

        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());

        long createAt = reader.GetInt64(0);
        long modifiedAt = reader.GetInt64(1);
        double bin001 = reader.GetDouble(2);
        return (createAt, modifiedAt, bin001);
    }

    private static long ReadRowCount(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM \"T_STFTAnalysis\";";
        object? scalar = command.ExecuteScalar();
        return scalar is long count ? count : 0;
    }

    private static string ReadJournalMode(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        object? scalar = command.ExecuteScalar();
        return scalar as string ?? string.Empty;
    }

    private static bool IsKnownJournalMode(string mode)
    {
        return mode.Equals("wal", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("delete", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("truncate", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("persist", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("memory", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDbPath()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), $"sound-analyzer-stft-db-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, "analysis.db");
    }

    private static void DeleteIfExists(string dbFilePath)
    {
        string? directoryPath = Path.GetDirectoryName(dbFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        const int maxRetry = 5;
        for (int i = 0; i < maxRetry; i++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (IOException) when (i < maxRetry - 1)
            {
                _ = SpinWait.SpinUntil(static () => false, TimeSpan.FromMilliseconds(20));
            }
            catch (UnauthorizedAccessException) when (i < maxRetry - 1)
            {
                _ = SpinWait.SpinUntil(static () => false, TimeSpan.FromMilliseconds(20));
            }
        }
    }
}
