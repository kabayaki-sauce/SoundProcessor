using Cli.Shared.Application.Ports;
using SoundAnalyzer.Cli.Infrastructure.Progress;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Progress;

public sealed class SoundAnalyzerProgressTrackerTests
{
    [Fact]
    public void Complete_ShouldRenderMergedGauge_WithInsertAnalyzeAndPendingSegments_WhenExpectedIsKnown()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

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

        string workerLine = FindLine(display.LatestLines, "T01");
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.Contains("▓", workerLine, StringComparison.Ordinal);
        Assert.Contains("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A  60.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I  30.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderUnknownAsPending_WhenAnalyzeIsIncomplete()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

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

        string workerLine = FindLine(display.LatestLines, "T01");
        Assert.DoesNotContain("█", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("▓", workerLine, StringComparison.Ordinal);
        Assert.Contains("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A   0.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I   0.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldUseInsertedOverEnqueuedAfterAnalyzeComplete_WhenExpectedIsUnknown()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

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

        string workerLine = FindLine(display.LatestLines, "T01");
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.Contains("▓", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A 100.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I  40.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderAllGreen_WhenAnalyzeAndInsertAreComplete()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

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

        string workerLine = FindLine(display.LatestLines, "T01");
        Assert.Contains("█", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("▓", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("░", workerLine, StringComparison.Ordinal);
        Assert.Contains("A 100.0%", workerLine, StringComparison.Ordinal);
        Assert.Contains("I 100.0%", workerLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldAlignGaugeStartColumns_ForSongsQueueAndThread()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

        tracker.Configure(["long_song_name"], threadCount: 1, queueCapacity: 256);
        tracker.SetWorkerSong(0, "long_song_name");
        tracker.SetSongExpectedPoints("long_song_name", expectedPoints: 100);
        tracker.IncrementEnqueued("long_song_name");
        tracker.Complete();

        string songsLine = FindLine(display.LatestLines, "Songs ");
        string queueLine = FindLine(display.LatestLines, "Queue ");
        string workerLine = FindLine(display.LatestLines, "T01");

        int songsGaugeStart = songsLine.IndexOf('|', StringComparison.Ordinal);
        int queueGaugeStart = queueLine.IndexOf('|', StringComparison.Ordinal);
        int workerGaugeStart = workerLine.IndexOf('|', StringComparison.Ordinal);

        Assert.Equal(songsGaugeStart, queueGaugeStart);
        Assert.Equal(songsGaugeStart, workerGaugeStart);
    }

    [Fact]
    public void Complete_ShouldUseFixedNameColumnWidth()
    {
        CapturingTextBlockProgressDisplay display = new();
        using SoundAnalyzerProgressTracker tracker = SoundAnalyzerProgressTracker.CreateForTest(
            display,
            ansiEnabled: false);

        tracker.Configure(["abcdefghijklmnop"], threadCount: 1, queueCapacity: 256);
        tracker.SetWorkerSong(0, "abcdefghijklmnop");
        tracker.SetSongExpectedPoints("abcdefghijklmnop", expectedPoints: 100);
        tracker.Complete();

        string workerLine = FindLine(display.LatestLines, "T01");
        Assert.Contains("abcdefghijkl", workerLine, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnop", workerLine, StringComparison.Ordinal);
    }

    private static string FindLine(IReadOnlyList<string> lines, string prefix)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(prefix, StringComparison.Ordinal))
            {
                return lines[i];
            }
        }

        return string.Empty;
    }

    private sealed class CapturingTextBlockProgressDisplay : ITextBlockProgressDisplay
    {
        public IReadOnlyList<string> LatestLines { get; private set; } = Array.Empty<string>();

        public void Report(IReadOnlyList<string> lines, bool force = false)
        {
            _ = force;
            LatestLines = lines.ToArray();
        }

        public void Complete()
        {
        }
    }
}
