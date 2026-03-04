namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class MelSpectrogramAnalysisBatchSummary
{
    public MelSpectrogramAnalysisBatchSummary(
        int directoryCount,
        int analyzedFileCount,
        long writtenPointCount,
        string tableName,
        int melBinCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(analyzedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(writtenPointCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melBinCount);

        DirectoryCount = directoryCount;
        AnalyzedFileCount = analyzedFileCount;
        WrittenPointCount = writtenPointCount;
        TableName = tableName;
        MelBinCount = melBinCount;
    }

    public int DirectoryCount { get; }

    public int AnalyzedFileCount { get; }

    public long WrittenPointCount { get; }

    public string TableName { get; }

    public int MelBinCount { get; }
}
