using Microsoft.Data.Sqlite;
using SFFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Sqlite;

public sealed class SqliteSfftAnalysisStoreTests
{
    [Fact]
    public void Initialize_ShouldCreateTable_WhenDatabaseDoesNotExist()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteSfftAnalysisStore store = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Complete();
            }

            Assert.True(File.Exists(dbFilePath));
            Assert.True(TableExists(dbFilePath, "T_SFFTAnalysis"));
            Assert.Equal(12, CountBinColumns(dbFilePath));
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
            using (SqliteSfftAnalysisStore first = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Error,
                       binCount: 8,
                       deleteCurrent: false))
            {
                first.Initialize();
                first.Complete();
            }

            using SqliteSfftAnalysisStore second = new(
                dbFilePath,
                "T_SFFTAnalysis",
                SqliteConflictMode.Error,
                binCount: 12,
                deleteCurrent: false);

            CliException exception = Assert.Throws<CliException>(() => second.Initialize());
            Assert.Equal(CliErrorCode.SfftTableBinCountMismatch, exception.ErrorCode);
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
            using (SqliteSfftAnalysisStore store = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Complete();
            }

            string mode = ReadJournalMode(dbFilePath);
            Assert.Equal("wal", mode, ignoreCase: true);
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
            using (SqliteSfftAnalysisStore first = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Error,
                       binCount: 8,
                       deleteCurrent: false))
            {
                first.Initialize();
                first.Complete();
            }

            using (SqliteSfftAnalysisStore second = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Error,
                       binCount: 12,
                       deleteCurrent: true))
            {
                second.Initialize();
                second.Complete();
            }

            Assert.Equal(12, CountBinColumns(dbFilePath));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Write_ShouldUpsertAndKeepCreateAt()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            SfftAnalysisPoint firstPoint = new("Song", 0, 50, 10, CreateBins(4, -10));

            using (SqliteSfftAnalysisStore store = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Write(firstPoint);
                store.Complete();
            }

            (long createAtBefore, long modifiedAtBefore, double bin001Before) = ReadSingleRow(dbFilePath);
            _ = SpinWait.SpinUntil(
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > modifiedAtBefore,
                TimeSpan.FromMilliseconds(200));

            SfftAnalysisPoint updatedPoint = new("Song", 0, 50, 10, CreateBins(4, -3));
            using (SqliteSfftAnalysisStore store = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
                       SqliteConflictMode.Upsert,
                       binCount: 4,
                       deleteCurrent: false))
            {
                store.Initialize();
                store.Write(updatedPoint);
                store.Complete();
            }

            (long createAtAfter, long modifiedAtAfter, double bin001After) = ReadSingleRow(dbFilePath);

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
    public void Write_ShouldSkipDuplicate_WhenSkipDuplicateIsEnabled()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            SfftAnalysisPoint point = new("Song", 0, 50, 10, CreateBins(4, -10));

            using (SqliteSfftAnalysisStore store = new(
                       dbFilePath,
                       "T_SFFTAnalysis",
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

    private static int CountBinColumns(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"T_SFFTAnalysis\");";

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

    private static (long CreateAt, long ModifiedAt, double Bin001) ReadSingleRow(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT create_at, modified_at, bin001 FROM \"T_SFFTAnalysis\" WHERE name = 'Song' AND ch = 0 AND window = 50 AND ms = 10;";

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
        command.CommandText = "SELECT COUNT(1) FROM \"T_SFFTAnalysis\";";
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

    private static string CreateTempDbPath()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), $"sound-analyzer-sfft-db-tests-{Guid.NewGuid():N}");
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
