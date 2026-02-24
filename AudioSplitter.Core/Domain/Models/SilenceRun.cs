namespace AudioSplitter.Core.Domain.Models;

public sealed class SilenceRun
{
    public SilenceRun(long startFrame, long endFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endFrame, startFrame);

        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public long StartFrame { get; }

    public long EndFrame { get; }
}
