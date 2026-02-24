namespace AudioSplitter.Core.Domain.Models;

public sealed class SplitExecutionSummary
{
    public SplitExecutionSummary(
        int generatedCount,
        int skippedCount,
        int promptedCount,
        int detectedSegmentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generatedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(promptedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(detectedSegmentCount);

        GeneratedCount = generatedCount;
        SkippedCount = skippedCount;
        PromptedCount = promptedCount;
        DetectedSegmentCount = detectedSegmentCount;
    }

    public int GeneratedCount { get; }

    public int SkippedCount { get; }

    public int PromptedCount { get; }

    public int DetectedSegmentCount { get; }
}
