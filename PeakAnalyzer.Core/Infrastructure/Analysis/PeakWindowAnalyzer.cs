using AudioProcessor.Application.Ports;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Domain.Models;

namespace PeakAnalyzer.Core.Infrastructure.Analysis;

internal sealed class PeakWindowAnalyzer : IAudioPcmFrameSink
{
    private readonly int sampleRate;
    private readonly string name;
    private readonly string stem;
    private readonly long windowMs;
    private readonly long hopMs;
    private readonly double minLimitDb;
    private readonly IPeakAnalysisPointWriter pointWriter;

    private readonly LinkedList<PeakFrame> maxDeque = new();

    private long frameIndex;

    private int pointCount;

    private long lastMs;

    private long nextAnchorMs;

    private readonly long windowFrames;

    public PeakWindowAnalyzer(
        int sampleRate,
        string name,
        string stem,
        long windowMs,
        long hopMs,
        double minLimitDb,
        IPeakAnalysisPointWriter pointWriter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);
        ArgumentNullException.ThrowIfNull(pointWriter);

        this.sampleRate = sampleRate;
        this.name = name;
        this.stem = stem;
        this.windowMs = windowMs;
        this.hopMs = hopMs;
        this.minLimitDb = minLimitDb;
        this.pointWriter = pointWriter;

        long calculatedWindowFrames = TimeMsToFrameFloor(windowMs, sampleRate);
        windowFrames = Math.Max(1, calculatedWindowFrames);
        nextAnchorMs = hopMs;
    }

    public void OnFrame(ReadOnlySpan<float> frameSamples)
    {
        double framePeak = 0;
        for (int i = 0; i < frameSamples.Length; i++)
        {
            double sample = Math.Abs(frameSamples[i]);
            if (sample > framePeak)
            {
                framePeak = sample;
            }
        }

        EnqueuePeak(frameIndex, framePeak);
        frameIndex++;

        long elapsedMs = FrameToElapsedMsFloor(frameIndex, sampleRate);
        while (nextAnchorMs <= elapsedMs)
        {
            EmitPoint(nextAnchorMs);
            nextAnchorMs += hopMs;
        }
    }

    public PeakAnalysisSummary BuildSummary()
    {
        return new PeakAnalysisSummary(pointCount, frameIndex, lastMs);
    }

    private void EmitPoint(long anchorMs)
    {
        long endFrameExclusive = TimeMsToFrameFloor(anchorMs, sampleRate);
        long startFrame = endFrameExclusive - windowFrames;
        long minWindowFrame = Math.Max(0, startFrame);

        while (maxDeque.First is not null && maxDeque.First.Value.FrameIndex < minWindowFrame)
        {
            maxDeque.RemoveFirst();
        }

        double peak = maxDeque.First?.Value.Peak ?? 0;
        double db = peak <= 0 ? double.NegativeInfinity : 20 * Math.Log10(peak);
        if (db < minLimitDb)
        {
            db = minLimitDb;
        }

        PeakAnalysisPoint point = new(name, stem, windowMs, anchorMs, db);
        pointWriter.Write(point);

        pointCount++;
        lastMs = anchorMs;
    }

    private void EnqueuePeak(long currentFrameIndex, double framePeak)
    {
        while (maxDeque.Last is not null && maxDeque.Last.Value.Peak <= framePeak)
        {
            maxDeque.RemoveLast();
        }

        maxDeque.AddLast(new PeakFrame(currentFrameIndex, framePeak));
    }

    private static long FrameToElapsedMsFloor(long frameCount, int sampleRate)
    {
        return checked(frameCount * 1000 / sampleRate);
    }

    private static long TimeMsToFrameFloor(long ms, int sampleRate)
    {
        return checked(ms * sampleRate / 1000);
    }

    private readonly record struct PeakFrame(long FrameIndex, double Peak);
}
