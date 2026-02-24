using System.Globalization;

namespace AudioProcessor.Domain.Services;

public static class FrameMath
{
    public static long DurationToFrameThreshold(TimeSpan duration, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        double frames = duration.TotalSeconds * sampleRate;
        long threshold = checked((long)Math.Ceiling(frames));
        return Math.Max(1, threshold);
    }

    public static long TimeOffsetToFrames(TimeSpan offset, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        double frames = offset.TotalSeconds * sampleRate;
        return checked((long)Math.Round(frames, MidpointRounding.AwayFromZero));
    }

    public static long ClampFrame(long frame, long min, long max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);

        if (frame < min)
        {
            return min;
        }

        if (frame > max)
        {
            return max;
        }

        return frame;
    }

    public static string ToInvariantSeconds(long frame, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        double seconds = (double)frame / sampleRate;
        return seconds.ToString("0.################", CultureInfo.InvariantCulture);
    }
}
