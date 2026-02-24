using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Models;
using Cli.Shared.Application.Ports;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Application.UseCases;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class StftAnalysisBatchExecutor
{
    private readonly StftAnalysisUseCase stftAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;

    public StftAnalysisBatchExecutor(
        StftAnalysisUseCase stftAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService)
    {
        this.stftAnalysisUseCase = stftAnalysisUseCase ?? throw new ArgumentNullException(nameof(stftAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
    }

    public async Task<StftAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(arguments, SilentProgressDisplay.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StftAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        IProgressDisplay progressDisplay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(progressDisplay);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        int binCount = arguments.BinCount ?? throw new CliException(CliErrorCode.UnsupportedMode, arguments.Mode);
        string anchorColumnName = arguments.HopUnit == AnalysisLengthUnit.Sample ? "sample" : "ms";

        BatchExecutionSupport.EnsureDbDirectory(arguments.DbFilePath);

        ResolvedStftAudioFiles resolved = StftAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Recursive);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);

        long writtenPointCount = 0;
        using SqliteStftAnalysisStore store = new(
            arguments.DbFilePath,
            arguments.TableName,
            anchorColumnName,
            conflictMode,
            binCount,
            arguments.DeleteCurrent);
        store.Initialize();

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(arguments.FfmpegPath);
        long completedSongCount = 0;
        long totalSongCount = resolved.Files.Count;

        for (int index = 0; index < resolved.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            StftAudioFile target = resolved.Files[index];
            AudioStreamInfo streamInfo = await audioProbeService
                .ProbeAsync(toolPaths, target.FilePath, cancellationToken)
                .ConfigureAwait(false);

            int analysisSampleRate = arguments.TargetSamplingHz ?? streamInfo.SampleRate;
            long windowSamples = ResolveSamples(arguments.WindowValue, arguments.WindowUnit, analysisSampleRate);
            long hopSamples = ResolveSamples(arguments.HopValue, arguments.HopUnit, analysisSampleRate);

            long anchorCount = BatchExecutionSupport.EstimateAnchorCountBySamples(streamInfo, analysisSampleRate, hopSamples);
            long estimatedPointCount = checked(anchorCount * streamInfo.Channels);
            long safeSongTotal = estimatedPointCount > 0 ? estimatedPointCount : 1;
            long writtenForSong = 0;

            ReportSongProgress(
                progressDisplay,
                target.Name,
                completedSongCount,
                totalSongCount,
                writtenForSong,
                safeSongTotal);

            StftAnalysisRequest request = new(
                target.FilePath,
                target.Name,
                windowSamples,
                hopSamples,
                analysisSampleRate,
                arguments.HopUnit == AnalysisLengthUnit.Sample ? StftAnchorUnit.Sample : StftAnchorUnit.Millisecond,
                arguments.WindowValue,
                binCount,
                arguments.MinLimitDb,
                arguments.FfmpegPath);

            ProgressReportingStftWriter reportingWriter = new(
                store,
                pointCount =>
                {
                    writtenForSong = checked(writtenForSong + pointCount);
                    ReportSongProgress(
                        progressDisplay,
                        target.Name,
                        completedSongCount,
                        totalSongCount,
                        writtenForSong,
                        safeSongTotal);
                });

            StftAnalysisSummary summary = await stftAnalysisUseCase
                .ExecuteAsync(request, reportingWriter, cancellationToken)
                .ConfigureAwait(false);

            writtenPointCount = checked(writtenPointCount + summary.PointCount);
            completedSongCount++;
            ReportSongProgress(
                progressDisplay,
                target.Name,
                completedSongCount,
                totalSongCount,
                safeSongTotal,
                safeSongTotal);
        }

        if (resolved.Files.Count == 0)
        {
            BatchExecutionSupport.ReportDualProgress(
                progressDisplay,
                "Songs 0/0",
                1,
                1,
                "Current song",
                1,
                1);
        }

        store.Complete();

        return new StftAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            writtenPointCount,
            arguments.TableName,
            binCount);
    }

    private static long ResolveSamples(long value, AnalysisLengthUnit unit, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        return unit == AnalysisLengthUnit.Sample
            ? value
            : BatchExecutionSupport.ConvertDurationMsToSamples(value, sampleRate);
    }

    private static void ReportSongProgress(
        IProgressDisplay progressDisplay,
        string songName,
        long completedSongCount,
        long totalSongCount,
        long songWrittenPoints,
        long songTotalPoints)
    {
        ArgumentNullException.ThrowIfNull(progressDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);
        ArgumentOutOfRangeException.ThrowIfNegative(completedSongCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalSongCount);
        ArgumentOutOfRangeException.ThrowIfNegative(songWrittenPoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(songTotalPoints);

        long safeTopTotal = totalSongCount > 0 ? totalSongCount : 1;
        const long topScale = 1000;
        long topTotalScaled = checked(safeTopTotal * topScale);
        long topProcessedScaled = totalSongCount > 0
            ? checked(completedSongCount * topScale)
            : topScale;

        double songRatio = BatchExecutionSupport.ToRatio(songWrittenPoints, songTotalPoints);
        if (totalSongCount > 0 && completedSongCount < safeTopTotal)
        {
            topProcessedScaled = Math.Min(
                topTotalScaled,
                checked(topProcessedScaled + (long)Math.Round(songRatio * topScale, MidpointRounding.AwayFromZero)));
        }

        string topLabel = string.Create(
            CultureInfo.InvariantCulture,
            $"Songs {Math.Min(completedSongCount, safeTopTotal)}/{safeTopTotal} [{songName}]");
        string bottomLabel = string.Create(
            CultureInfo.InvariantCulture,
            $"{songName} points {Math.Min(songWrittenPoints, songTotalPoints)}/{songTotalPoints}");

        BatchExecutionSupport.ReportDualProgress(
            progressDisplay,
            topLabel,
            topProcessedScaled,
            topTotalScaled,
            bottomLabel,
            songWrittenPoints,
            songTotalPoints);
    }

    private sealed class ProgressReportingStftWriter : IStftAnalysisPointWriter
    {
        private readonly IStftAnalysisPointWriter inner;
        private readonly Action<int> onWrite;

        public ProgressReportingStftWriter(IStftAnalysisPointWriter inner, Action<int> onWrite)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.onWrite = onWrite ?? throw new ArgumentNullException(nameof(onWrite));
        }

        public void Write(StftAnalysisPoint point)
        {
            inner.Write(point);
            onWrite(1);
        }
    }

    private sealed class SilentProgressDisplay : IProgressDisplay
    {
        public static SilentProgressDisplay Instance { get; } = new();

        private SilentProgressDisplay()
        {
        }

        public void Report(DualProgressState state)
        {
            _ = state;
        }

        public void Complete()
        {
        }
    }
}
