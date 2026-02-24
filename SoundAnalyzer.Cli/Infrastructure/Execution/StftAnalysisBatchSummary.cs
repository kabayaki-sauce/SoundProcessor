namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class StftAnalysisBatchSummary
{
    public StftAnalysisBatchSummary(
        int directoryCount,
        int analyzedFileCount,
        long writtenPointCount,
        string tableName,
        int binCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(analyzedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(writtenPointCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        DirectoryCount = directoryCount;
        AnalyzedFileCount = analyzedFileCount;
        WrittenPointCount = writtenPointCount;
        TableName = tableName;
        BinCount = binCount;
    }

    public int DirectoryCount { get; }

    public int AnalyzedFileCount { get; }

    public long WrittenPointCount { get; }

    public string TableName { get; }

    public int BinCount { get; }
}
