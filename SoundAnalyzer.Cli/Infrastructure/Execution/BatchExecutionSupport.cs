using AudioProcessor.Domain.Models;
using CliShared.Application.Models;
using CliShared.Application.Ports;
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
