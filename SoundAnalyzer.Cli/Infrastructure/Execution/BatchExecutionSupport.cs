using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Models;
using Cli.Shared.Application.Ports;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal static class BatchExecutionSupport
{
    public static SqliteConflictMode ResolveConflictMode(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Upsert)
        {
            return SqliteConflictMode.Upsert;
        }

        if (arguments.SkipDuplicate)
        {
            return SqliteConflictMode.SkipDuplicate;
        }

        return SqliteConflictMode.Error;
    }

    public static void EnsureDbDirectory(string dbFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);

        string? directoryPath = Path.GetDirectoryName(dbFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CliException(CliErrorCode.DbDirectoryCreationFailed, directoryPath, ex);
        }
    }

    public static long EstimateAnchorCount(AudioStreamInfo streamInfo, long hopMs)
    {
        ArgumentNullException.ThrowIfNull(streamInfo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);

        if (!streamInfo.EstimatedTotalFrames.HasValue)
        {
            return 0;
        }

        long estimatedFrames = streamInfo.EstimatedTotalFrames.Value;
        long elapsedMs = checked(estimatedFrames * 1000 / streamInfo.SampleRate);
        if (elapsedMs <= 0)
        {
            return 0;
        }

        return elapsedMs / hopMs;
    }

    public static long EstimateAnchorCountBySamples(
        AudioStreamInfo streamInfo,
        int analysisSampleRate,
        long hopSamples)
    {
        ArgumentNullException.ThrowIfNull(streamInfo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(analysisSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopSamples);

        if (!streamInfo.EstimatedTotalFrames.HasValue)
        {
            return 0;
        }

        long estimatedFrames = ScaleFrameCount(streamInfo.EstimatedTotalFrames.Value, streamInfo.SampleRate, analysisSampleRate);
        if (estimatedFrames <= 0)
        {
            return 0;
        }

        return estimatedFrames / hopSamples;
    }

    public static long ConvertDurationMsToSamples(long durationMs, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        long converted = checked(durationMs * sampleRate / 1000);
        return Math.Max(1, converted);
    }

    public static long ScaleFrameCount(long frameCount, int sourceSampleRate, int targetSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRate);

        if (sourceSampleRate == targetSampleRate)
        {
            return frameCount;
        }

        return checked(frameCount * targetSampleRate / sourceSampleRate);
    }

    public static double ToRatio(long processed, long? total)
    {
        if (!total.HasValue || total.Value <= 0)
        {
            return 0;
        }

        double ratio = (double)processed / total.Value;
        if (ratio <= 0)
        {
            return 0;
        }

        if (ratio >= 1)
        {
            return 1;
        }

        return ratio;
    }

    public static void ReportDualProgress(
        IProgressDisplay progressDisplay,
        string topLabel,
        long topProcessed,
        long? topTotal,
        string bottomLabel,
        long bottomProcessed,
        long? bottomTotal)
    {
        ArgumentNullException.ThrowIfNull(progressDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(topLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(bottomLabel);

        progressDisplay.Report(
            new DualProgressState(
                topLabel,
                ToRatio(topProcessed, topTotal),
                bottomLabel,
                ToRatio(bottomProcessed, bottomTotal)));
    }
}
