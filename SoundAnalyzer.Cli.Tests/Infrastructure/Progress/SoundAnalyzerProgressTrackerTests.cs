using SoundAnalyzer.Cli.Infrastructure.Progress;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Progress;

public sealed class SoundAnalyzerProgressTrackerTests
{
    [Fact]
    public void Complete_ShouldRenderMergedGauge_WithInsertAnalyzeAndPendingSegments_WhenExpectedIsKnown()
    {
        StringWriter writer = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            ansiEnabled: false,
            writer);

        tracker.Configure(["song_001"], threadCount: 1, queueCapacity: 1024);
        tracker.SetWorkerSong(0, "song_001");
        tracker.SetSongExpectedPoints("song_001", expectedPoints: 10);

        for (int i = 0; i < 6; i++)
        {
            tracker.IncrementEnqueued("song_001");
        }

        for (int i = 0; i < 3; i++)
        {
            tracker.IncrementInserted("song_001");
        }

        tracker.Complete();

        string workerLine = FindLastWorkerLine(writer.ToString());
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.Contains("▓", workerLine, StringComparison.Ordinal);
        Assert.Contains("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A  60.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I  30.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderUnknownAsPending_WhenAnalyzeIsIncomplete()
    {
        StringWriter writer = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            ansiEnabled: false,
            writer);

        tracker.Configure(["song_002"], threadCount: 1, queueCapacity: 1024);
        tracker.SetWorkerSong(0, "song_002");
        tracker.MarkSongExpectedPointsUnknown("song_002");

        for (int i = 0; i < 5; i++)
        {
            tracker.IncrementEnqueued("song_002");
        }

        for (int i = 0; i < 2; i++)
        {
            tracker.IncrementInserted("song_002");
        }

        tracker.Complete();

        string workerLine = FindLastWorkerLine(writer.ToString());
        Assert.DoesNotContain("█", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("▓", workerLine, StringComparison.Ordinal);
        Assert.Contains("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A   0.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I   0.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldUseInsertedOverEnqueuedAfterAnalyzeComplete_WhenExpectedIsUnknown()
    {
        StringWriter writer = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            ansiEnabled: false,
            writer);

        tracker.Configure(["song_003"], threadCount: 1, queueCapacity: 1024);
        tracker.SetWorkerSong(0, "song_003");
        tracker.MarkSongExpectedPointsUnknown("song_003");

        for (int i = 0; i < 10; i++)
        {
            tracker.IncrementEnqueued("song_003");
        }

        for (int i = 0; i < 4; i++)
        {
            tracker.IncrementInserted("song_003");
        }

        tracker.MarkWorkerAnalyzeCompleted(0);
        tracker.Complete();

        string workerLine = FindLastWorkerLine(writer.ToString());
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.Contains("▓", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A 100.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I  40.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderAllGreen_WhenAnalyzeAndInsertAreComplete()
    {
        StringWriter writer = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            ansiEnabled: false,
            writer);

        tracker.Configure(["song_004"], threadCount: 1, queueCapacity: 1024);
        tracker.SetWorkerSong(0, "song_004");
        tracker.SetSongExpectedPoints("song_004", expectedPoints: 10);

        for (int i = 0; i < 10; i++)
        {
            tracker.IncrementEnqueued("song_004");
            tracker.IncrementInserted("song_004");
        }

        tracker.MarkWorkerAnalyzeCompleted(0);
        tracker.Complete();

        string workerLine = FindLastWorkerLine(writer.ToString());
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("▓", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A 100.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I 100.0%", workerLine, StringComparison.Ordinal);
    }

    private static string FindLastWorkerLine(string allText)
    {
        string[] lines = allText.Split(Environment.NewLine, StringSplitOptions.None);
        for (int index = lines.Length - 1; index >= 0; index--)
        {
            if (lines[index].Contains("T01", StringComparison.Ordinal))
            {
                return lines[index];
            }
        }

        return string.Empty;
    }
}
