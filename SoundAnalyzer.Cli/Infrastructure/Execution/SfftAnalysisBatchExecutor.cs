using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Models;
using Cli.Shared.Application.Ports;
using SFFTAnalyzer.Core.Application.Models;
using SFFTAnalyzer.Core.Application.Ports;
using SFFTAnalyzer.Core.Application.UseCases;
using SFFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class SfftAnalysisBatchExecutor
{
    private readonly SfftAnalysisUseCase sfftAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;

    public SfftAnalysisBatchExecutor(
        SfftAnalysisUseCase sfftAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService)
    {
        this.sfftAnalysisUseCase = sfftAnalysisUseCase ?? throw new ArgumentNullException(nameof(sfftAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
    }

    public async Task<SfftAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(arguments, SilentProgressDisplay.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SfftAnalysisBatchSummary> ExecuteAsync(
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

        BatchExecutionSupport.EnsureDbDirectory(arguments.DbFilePath);

        ResolvedSfftAudioFiles resolved = SfftAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Recursive);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);

        long writtenPointCount = 0;
        using SqliteSfftAnalysisStore store = new(
            arguments.DbFilePath,
            arguments.TableName,
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

            SfftAudioFile target = resolved.Files[index];
            AudioStreamInfo streamInfo = await audioProbeService
                .ProbeAsync(toolPaths, target.FilePath, cancellationToken)
                .ConfigureAwait(false);

            long anchorCount = BatchExecutionSupport.EstimateAnchorCount(streamInfo, arguments.HopMs);
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

            SfftAnalysisRequest request = new(
                target.FilePath,
                target.Name,
                arguments.WindowSizeMs,
                arguments.HopMs,
                binCount,
                arguments.MinLimitDb,
                arguments.FfmpegPath);

            ProgressReportingSfftWriter reportingWriter = new(
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

            SfftAnalysisSummary summary = await sfftAnalysisUseCase
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

        return new SfftAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            writtenPointCount,
            arguments.TableName,
            binCount);
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

    private sealed class ProgressReportingSfftWriter : ISfftAnalysisPointWriter
    {
        private readonly ISfftAnalysisPointWriter inner;
        private readonly Action<int> onWrite;

        public ProgressReportingSfftWriter(ISfftAnalysisPointWriter inner, Action<int> onWrite)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.onWrite = onWrite ?? throw new ArgumentNullException(nameof(onWrite));
        }

        public void Write(SfftAnalysisPoint point)
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
