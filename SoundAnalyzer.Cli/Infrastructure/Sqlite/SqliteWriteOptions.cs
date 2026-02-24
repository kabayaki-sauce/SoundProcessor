namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

internal sealed class SqliteWriteOptions
{
    public SqliteWriteOptions(bool fastMode, int batchRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchRowCount);

        FastMode = fastMode;
        BatchRowCount = batchRowCount;
    }

    public bool FastMode { get; }

    public int BatchRowCount { get; }

    public static SqliteWriteOptions Default { get; } = new(fastMode: false, batchRowCount: 512);
}
