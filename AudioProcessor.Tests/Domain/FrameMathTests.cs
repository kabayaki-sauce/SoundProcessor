using AudioProcessor.Domain.Services;

namespace AudioProcessor.Tests.Domain;

public sealed class FrameMathTests
{
    [Theory]
    [InlineData(2000, 44100, 88200)]
    [InlineData(1, 44100, 45)]
    [InlineData(100, 48000, 4800)]
    public void DurationToFrameThreshold_ShouldCeil(double milliseconds, int sampleRate, long expected)
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(milliseconds);

        long actual = FrameMath.DurationToFrameThreshold(duration, sampleRate);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0.5, 44100, 22)]
    [InlineData(-0.5, 44100, -22)]
    [InlineData(1.0, 44100, 44)]
    public void TimeOffsetToFrames_ShouldRoundAwayFromZero(double milliseconds, int sampleRate, long expected)
    {
        TimeSpan offset = TimeSpan.FromMilliseconds(milliseconds);

        long actual = FrameMath.TimeOffsetToFrames(offset, sampleRate);

        Assert.Equal(expected, actual);
    }
}
