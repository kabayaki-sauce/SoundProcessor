using AudioProcessor.Domain.Models;
using AudioProcessor.Domain.Services;
using AudioSplitter.Core.Domain.Models;

namespace AudioSplitter.Core.Domain.Services;

internal static class SegmentPlanner
{
    public static IReadOnlyList<AudioSegment> Build(
        SilenceAnalysisResult analysisResult,
        int sampleRate,
        TimeSpan afterOffset,
        TimeSpan resumeOffset)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        if (!analysisResult.FirstSoundFrame.HasValue)
        {
            return Array.Empty<AudioSegment>();
        }

        long totalFrames = analysisResult.TotalFrames;
        long firstSoundFrame = analysisResult.FirstSoundFrame.Value;
        long afterOffsetFrames = FrameMath.TimeOffsetToFrames(afterOffset, sampleRate);
        long resumeOffsetFrames = FrameMath.TimeOffsetToFrames(resumeOffset, sampleRate);

        List<AudioSegment> segments = new();
        long currentStart = FrameMath.ClampFrame(firstSoundFrame + resumeOffsetFrames, 0, totalFrames);
        for (int i = 0; i < analysisResult.SilenceRuns.Count; i++)
        {
            SilenceRun run = analysisResult.SilenceRuns[i];
            long previousEnd = FrameMath.ClampFrame(run.StartFrame + afterOffsetFrames, 0, totalFrames);
            AddIfValid(segments, currentStart, previousEnd);

            long nextStart = FrameMath.ClampFrame(run.EndFrame + resumeOffsetFrames, 0, totalFrames);
            currentStart = nextStart;
        }

        long finalEnd = totalFrames;
        if (analysisResult.SilenceRuns.Count > 0)
        {
            SilenceRun lastRun = analysisResult.SilenceRuns[^1];
            if (lastRun.EndFrame == totalFrames)
            {
                finalEnd = FrameMath.ClampFrame(lastRun.StartFrame + afterOffsetFrames, 0, totalFrames);
            }
        }

        AddIfValid(segments, currentStart, finalEnd);
        return segments;
    }

    private static void AddIfValid(List<AudioSegment> segments, long startFrame, long endFrame)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (endFrame <= startFrame)
        {
            return;
        }

        segments.Add(new AudioSegment(startFrame, endFrame));
    }
}
