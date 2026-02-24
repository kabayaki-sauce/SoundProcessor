using AudioSplitter.Core.Domain.Models;
using AudioSplitter.Core.Domain.Services;

namespace AudioSplitter.Core.Tests.Domain;

public sealed class SegmentPlannerTests
{
    [Fact]
    public void Build_ShouldApplyAfterAndResumeOffsets()
    {
        SilenceAnalysisResult analysis = new(
            totalFrames: 10_000,
            firstSoundFrame: 100,
            silenceRuns: new[] { new SilenceRun(3_100, 6_100) });

        IReadOnlyList<AudioProcessor.Domain.Models.AudioSegment> segments = SegmentPlanner.Build(
            analysis,
            sampleRate: 1_000,
            afterOffset: TimeSpan.FromMilliseconds(500),
            resumeOffset: TimeSpan.FromMilliseconds(-200));

        Assert.Equal(2, segments.Count);
        Assert.Equal(0, segments[0].StartFrame);
        Assert.Equal(3_600, segments[0].EndFrame);
        Assert.Equal(5_900, segments[1].StartFrame);
        Assert.Equal(10_000, segments[1].EndFrame);
    }

    [Fact]
    public void Build_ShouldReturnEmpty_WhenAllSilent()
    {
        SilenceAnalysisResult analysis = new(
            totalFrames: 10_000,
            firstSoundFrame: null,
            silenceRuns: new[] { new SilenceRun(0, 10_000) });

        IReadOnlyList<AudioProcessor.Domain.Models.AudioSegment> segments = SegmentPlanner.Build(
            analysis,
            sampleRate: 48_000,
            afterOffset: TimeSpan.Zero,
            resumeOffset: TimeSpan.Zero);

        Assert.Empty(segments);
    }

    [Fact]
    public void Build_ShouldAllowOverlap_WhenOffsetsCross()
    {
        SilenceAnalysisResult analysis = new(
            totalFrames: 10_000,
            firstSoundFrame: 1_000,
            silenceRuns: new[] { new SilenceRun(3_000, 6_000) });

        IReadOnlyList<AudioProcessor.Domain.Models.AudioSegment> segments = SegmentPlanner.Build(
            analysis,
            sampleRate: 1_000,
            afterOffset: TimeSpan.FromMilliseconds(2_000),
            resumeOffset: TimeSpan.FromMilliseconds(-1_500));

        Assert.Equal(2, segments.Count);
        Assert.True(segments[0].EndFrame > segments[1].StartFrame);
    }
}
