namespace SFFTAnalyzer.Core.Domain.Models;

public sealed class SfftAnalysisSummary
{
    public SfftAnalysisSummary(int pointCount, long totalFrames, long lastMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pointCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(lastMs);

        PointCount = pointCount;
        TotalFrames = totalFrames;
        LastMs = lastMs;
    }

    public int PointCount { get; }

    public long TotalFrames { get; }

    public long LastMs { get; }
}
