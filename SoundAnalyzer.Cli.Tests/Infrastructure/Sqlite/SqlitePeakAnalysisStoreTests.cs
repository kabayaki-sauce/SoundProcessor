using Microsoft.Data.Sqlite;
using PeakAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Sqlite;

public sealed class SqlitePeakAnalysisStoreTests
{
    [Fact]
    public void Initialize_ShouldCreateTable_WhenDatabaseDoesNotExist()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqlitePeakAnalysisStore store = new(dbFilePath, "T_PeakAnalysis", SqliteConflictMode.Error))
            {
                store.Initialize();
                store.Complete();
            }

            Assert.True(File.Exists(dbFilePath));
            Assert.True(TableExists(dbFilePath, "T_PeakAnalysis"));
        }
        finally
        {
            DeleteIfExists(dbFilePath);
        }
    }

    [Fact]
    public void Initialize_ShouldRepairIndexes_WhenTableAlreadyExists()
    {
        string dbFilePath = CreateTempDbPath();
        try
        {
            using (SqliteConnection connection = OpenConnection(dbFilePath))
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE "T_PeakAnalysis" (
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
                    """;
                _ = command.ExecuteNonQuery();
            }

            using (SqlitePeakAnalysisStore store = new(dbFilePath, "T_PeakAnalysis", SqliteConflictMode.Error))
            {
                store.Initialize();
                store.Complete();
            }

            string[] indexNames = GetIndexNames(dbFilePath);
            Assert.Contains("IX_T_PeakAnalysis_name", indexNames, StringComparer.Ordinal);
            Assert.Contains("IX_T_PeakAnalysis_name_stem", indexNames, StringComparer.Ordinal);
            Assert.Contains("IX_T_PeakAnalysis_name_ms", indexNames, StringComparer.Ordinal);
            Assert.Contains("IX_T_PeakAnalysis_name_stem_ms", indexNames, StringComparer.Ordinal);
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
            PeakAnalysisPoint point = new("Album", "Piano", 50, 10, -12.0);

            using (SqlitePeakAnalysisStore store = new(dbFilePath, "T_PeakAnalysis", SqliteConflictMode.Upsert))
            {
                store.Initialize();
                store.Write(point);
                store.Complete();
            }

            (long createAtBefore, long modifiedAtBefore, double dbBefore) = ReadSingleRow(dbFilePath);
            _ = SpinWait.SpinUntil(
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > modifiedAtBefore,
                TimeSpan.FromMilliseconds(200));

            PeakAnalysisPoint updatedPoint = new("Album", "Piano", 50, 10, -6.0);
            using (SqlitePeakAnalysisStore store = new(dbFilePath, "T_PeakAnalysis", SqliteConflictMode.Upsert))
            {
                store.Initialize();
                store.Write(updatedPoint);
                store.Complete();
            }

            (long createAtAfter, long modifiedAtAfter, double dbAfter) = ReadSingleRow(dbFilePath);

            Assert.Equal(createAtBefore, createAtAfter);
            Assert.True(modifiedAtAfter >= modifiedAtBefore);
            Assert.NotEqual(dbBefore, dbAfter);
            Assert.Equal(-6.0, dbAfter, precision: 6);
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
            PeakAnalysisPoint point = new("Album", "Piano", 50, 10, -12.0);

            using (SqlitePeakAnalysisStore store = new(dbFilePath, "T_PeakAnalysis", SqliteConflictMode.SkipDuplicate))
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

    private static string[] GetIndexNames(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index';";

        List<string> names = new();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    private static (long CreateAt, long ModifiedAt, double Db) ReadSingleRow(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT create_at, modified_at, db FROM \"T_PeakAnalysis\" WHERE name = 'Album' AND stem = 'Piano' AND window = 50 AND ms = 10;";

        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());

        long createAt = reader.GetInt64(0);
        long modifiedAt = reader.GetInt64(1);
        double db = reader.GetDouble(2);
        return (createAt, modifiedAt, db);
    }

    private static long ReadRowCount(string dbFilePath)
    {
        using SqliteConnection connection = OpenConnection(dbFilePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM \"T_PeakAnalysis\";";
        object? scalar = command.ExecuteScalar();
        return scalar is long count ? count : 0;
    }

    private static string CreateTempDbPath()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), $"sound-analyzer-db-tests-{Guid.NewGuid():N}");
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
