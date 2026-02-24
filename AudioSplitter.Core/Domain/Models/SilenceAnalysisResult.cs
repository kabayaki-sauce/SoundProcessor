namespace AudioSplitter.Core.Domain.Models;

public sealed class SilenceAnalysisResult
{
    public SilenceAnalysisResult(
        long totalFrames,
        long? firstSoundFrame,
        IReadOnlyList<SilenceRun> silenceRuns)
    {
        ArgumentNullException.ThrowIfNull(silenceRuns);
        ArgumentOutOfRangeException.ThrowIfNegative(totalFrames);
        if (firstSoundFrame.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(firstSoundFrame.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(firstSoundFrame.Value, totalFrames);
        }

        TotalFrames = totalFrames;
        FirstSoundFrame = firstSoundFrame;
        SilenceRuns = silenceRuns;
    }

    public long TotalFrames { get; }

    public long? FirstSoundFrame { get; }

    public IReadOnlyList<SilenceRun> SilenceRuns { get; }
}
