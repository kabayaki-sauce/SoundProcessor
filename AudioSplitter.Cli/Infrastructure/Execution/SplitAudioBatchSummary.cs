namespace AudioSplitter.Cli.Infrastructure.Execution;

internal sealed class SplitAudioBatchSummary
{
    public SplitAudioBatchSummary(
        int processedFileCount,
        int generatedCount,
        int skippedCount,
        int promptedCount,
        int detectedSegmentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(generatedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(promptedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(detectedSegmentCount);

        ProcessedFileCount = processedFileCount;
        GeneratedCount = generatedCount;
        SkippedCount = skippedCount;
        PromptedCount = promptedCount;
        DetectedSegmentCount = detectedSegmentCount;
    }

    public int ProcessedFileCount { get; }

    public int GeneratedCount { get; }

    public int SkippedCount { get; }

    public int PromptedCount { get; }

    public int DetectedSegmentCount { get; }
}
