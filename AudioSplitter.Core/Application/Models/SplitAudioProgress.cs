namespace AudioSplitter.Core.Application.Models;

public readonly record struct SplitAudioProgress(
    SplitAudioPhase Phase,
    long Processed,
    long? Total)
{
    public SplitAudioPhase Phase { get; } = Phase;

    public long Processed { get; } = Processed >= 0 ? Processed : 0;

    public long? Total { get; } = NormalizeTotal(Total);

    private static long? NormalizeTotal(long? total)
    {
        if (!total.HasValue || total.Value <= 0)
        {
            return null;
        }

        return total;
    }
}
