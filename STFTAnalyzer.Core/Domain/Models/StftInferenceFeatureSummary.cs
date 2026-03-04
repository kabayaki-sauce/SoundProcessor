namespace STFTAnalyzer.Core.Domain.Models;

public sealed class StftInferenceFeatureSummary
{
    public StftInferenceFeatureSummary(int nowPointCount, int framePointCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nowPointCount);
        ArgumentOutOfRangeException.ThrowIfNegative(framePointCount);

        NowPointCount = nowPointCount;
        FramePointCount = framePointCount;
    }

    public int NowPointCount { get; }

    public int FramePointCount { get; }
}
