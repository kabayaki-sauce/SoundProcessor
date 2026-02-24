namespace AudioProcessor.Domain.Models;

public sealed class AudioSegment
{
    public AudioSegment(long startFrame, long endFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endFrame, startFrame);

        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public long StartFrame { get; }

    public long EndFrame { get; }
}
