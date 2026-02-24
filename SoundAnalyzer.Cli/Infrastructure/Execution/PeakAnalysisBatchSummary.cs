namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class PeakAnalysisBatchSummary
{
    public PeakAnalysisBatchSummary(
        int directoryCount,
        int analyzedFileCount,
        int skippedStemCount,
        long writtenPointCount,
        string tableName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(analyzedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedStemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(writtenPointCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        DirectoryCount = directoryCount;
        AnalyzedFileCount = analyzedFileCount;
        SkippedStemCount = skippedStemCount;
        WrittenPointCount = writtenPointCount;
        TableName = tableName;
    }

    public int DirectoryCount { get; }

    public int AnalyzedFileCount { get; }

    public int SkippedStemCount { get; }

    public long WrittenPointCount { get; }

    public string TableName { get; }
}
