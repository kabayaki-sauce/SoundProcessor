using AudioProcessor.Domain.Models;
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

        string resolvedDbFilePath = Path.GetFullPath(dbFilePath);
        string? directoryPath = Path.GetDirectoryName(resolvedDbFilePath);
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

    public static long ConvertDurationMsToSamples(long durationMs, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        long converted = checked(durationMs * sampleRate / 1000);
        return Math.Max(1, converted);
    }

    public static bool TryEstimatePeakPointCountPerTarget(
        AudioStreamInfo streamInfo,
        long hopMs,
        out long expectedPointCount)
    {
        ArgumentNullException.ThrowIfNull(streamInfo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);

        expectedPointCount = 0;
        if (!streamInfo.EstimatedTotalFrames.HasValue)
        {
            return false;
        }

        long totalFrames = streamInfo.EstimatedTotalFrames.Value;
        long totalMs = checked(totalFrames * 1000 / streamInfo.SampleRate);
        long estimatedPoints = totalMs / hopMs;
        if (estimatedPoints <= 0)
        {
            return false;
        }

        expectedPointCount = estimatedPoints;
        return true;
    }

    public static bool TryEstimateStftPointCountPerFile(
        AudioStreamInfo streamInfo,
        int analysisSampleRate,
        long hopSamples,
        out long expectedPointCount)
    {
        ArgumentNullException.ThrowIfNull(streamInfo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(analysisSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopSamples);

        expectedPointCount = 0;
        if (!streamInfo.EstimatedTotalFrames.HasValue)
        {
            return false;
        }

        long totalFrames = streamInfo.EstimatedTotalFrames.Value;
        long analysisFrames = checked(
            (long)Math.Round(
                (double)totalFrames * analysisSampleRate / streamInfo.SampleRate,
                MidpointRounding.AwayFromZero));
        long anchorCount = analysisFrames / hopSamples;
        long estimatedPoints = checked(anchorCount * streamInfo.Channels);
        if (estimatedPoints <= 0)
        {
            return false;
        }

        expectedPointCount = estimatedPoints;
        return true;
    }
}
