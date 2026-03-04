namespace MelSpectrogramAnalyzer.Core.Domain.Models;

public sealed class MelSpectrogramAnalysisSummary
{
    public MelSpectrogramAnalysisSummary(int pointCount, long totalFrames, long lastAnchor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pointCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(lastAnchor);

        PointCount = pointCount;
        TotalFrames = totalFrames;
        LastAnchor = lastAnchor;
    }

    public int PointCount { get; }

    public long TotalFrames { get; }

    public long LastAnchor { get; }
}

