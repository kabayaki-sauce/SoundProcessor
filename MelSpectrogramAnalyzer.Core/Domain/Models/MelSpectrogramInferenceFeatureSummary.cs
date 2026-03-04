namespace MelSpectrogramAnalyzer.Core.Domain.Models;

public sealed class MelSpectrogramInferenceFeatureSummary
{
    public MelSpectrogramInferenceFeatureSummary(int nowPointCount, int framePointCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nowPointCount);
        ArgumentOutOfRangeException.ThrowIfNegative(framePointCount);

        NowPointCount = nowPointCount;
        FramePointCount = framePointCount;
    }

    public int NowPointCount { get; }

    public int FramePointCount { get; }
}
