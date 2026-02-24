namespace AudioSplitter.Core.Application.Models;

public readonly record struct SilenceAnalysisProgress(long ProcessedFrames, long? TotalFrames)
{
    public long ProcessedFrames { get; } = ProcessedFrames >= 0 ? ProcessedFrames : 0;

    public long? TotalFrames { get; } = NormalizeTotal(TotalFrames);

    private static long? NormalizeTotal(long? totalFrames)
    {
        if (!totalFrames.HasValue || totalFrames.Value <= 0)
        {
            return null;
        }

        return totalFrames;
    }
}
