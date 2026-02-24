using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using CliShared.Application.Models;
using CliShared.Application.Ports;
using PeakAnalyzer.Core.Application.Models;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Application.UseCases;
using PeakAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class PeakAnalysisBatchExecutor
{
    private readonly PeakAnalysisUseCase peakAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;

    public PeakAnalysisBatchExecutor(
        PeakAnalysisUseCase peakAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService)
    {
        this.peakAnalysisUseCase = peakAnalysisUseCase ?? throw new ArgumentNullException(nameof(peakAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
    }

    public async Task<PeakAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(arguments, SilentProgressDisplay.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PeakAnalysisBatchSummary> ExecuteAsync(
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

        BatchExecutionSupport.EnsureDbDirectory(arguments.DbFilePath);

        ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Stems);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);

        long writtenPointCount = 0;
        using SqlitePeakAnalysisStore store = new(arguments.DbFilePath, arguments.TableName, conflictMode);
        store.Initialize();

        IReadOnlyList<SongBatch> songs = await BuildSongBatchesAsync(resolved.Files, arguments, cancellationToken)
            .ConfigureAwait(false);
        long completedSongCount = 0;

        for (int songIndex = 0; songIndex < songs.Count; songIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SongBatch song = songs[songIndex];
            long writtenForSong = 0;
            ReportSongProgress(progressDisplay, song.Name, songs.Count, completedSongCount, writtenForSong, song.EstimatedPointCount);

            for (int targetIndex = 0; targetIndex < song.Targets.Count; targetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StemAudioFile target = song.Targets[targetIndex];
                PeakAnalysisRequest request = new(
                    target.FilePath,
                    target.Name,
                    target.Stem,
                    arguments.WindowSizeMs,
                    arguments.HopMs,
                    arguments.MinLimitDb,
                    arguments.FfmpegPath);

                ProgressReportingPeakWriter reportingWriter = new(
                    store,
                    pointCount =>
                    {
                        writtenForSong = checked(writtenForSong + pointCount);
                        ReportSongProgress(
                            progressDisplay,
                            song.Name,
                            songs.Count,
                            completedSongCount,
                            writtenForSong,
                            song.EstimatedPointCount);
                    });

                PeakAnalysisSummary summary = await peakAnalysisUseCase
                    .ExecuteAsync(request, reportingWriter, cancellationToken)
                    .ConfigureAwait(false);

                writtenPointCount = checked(writtenPointCount + summary.PointCount);
            }

            completedSongCount++;
            ReportSongProgress(progressDisplay, song.Name, songs.Count, completedSongCount, song.EstimatedPointCount, song.EstimatedPointCount);
        }

        if (songs.Count == 0)
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

        return new PeakAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            resolved.SkippedStemCount,
            writtenPointCount,
            arguments.TableName);
    }

    private async Task<IReadOnlyList<SongBatch>> BuildSongBatchesAsync(
        IReadOnlyList<StemAudioFile> files,
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(arguments);

        Dictionary<string, SongBatchBuilder> builders = new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedNames = new();
        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(arguments.FfmpegPath);

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StemAudioFile file = files[i];
            if (!builders.TryGetValue(file.Name, out SongBatchBuilder? builder))
            {
                builder = new SongBatchBuilder(file.Name);
                builders[file.Name] = builder;
                orderedNames.Add(file.Name);
            }

            AudioStreamInfo streamInfo = await audioProbeService
                .ProbeAsync(toolPaths, file.FilePath, cancellationToken)
                .ConfigureAwait(false);
            long estimatedPointCount = BatchExecutionSupport.EstimateAnchorCount(streamInfo, arguments.HopMs);

            builder.Targets.Add(file);
            builder.EstimatedPointCount = checked(builder.EstimatedPointCount + estimatedPointCount);
        }

        List<SongBatch> songs = new(orderedNames.Count);
        for (int i = 0; i < orderedNames.Count; i++)
        {
            SongBatchBuilder builder = builders[orderedNames[i]];
            songs.Add(new SongBatch(builder.Name, builder.Targets, builder.EstimatedPointCount));
        }

        return songs;
    }

    private static void ReportSongProgress(
        IProgressDisplay progressDisplay,
        string songName,
        int totalSongs,
        long completedSongCount,
        long songWrittenPoints,
        long songEstimatedPoints)
    {
        ArgumentNullException.ThrowIfNull(progressDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);
        ArgumentOutOfRangeException.ThrowIfNegative(totalSongs);
        ArgumentOutOfRangeException.ThrowIfNegative(completedSongCount);
        ArgumentOutOfRangeException.ThrowIfNegative(songWrittenPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(songEstimatedPoints);

        long safeSongTotal = songEstimatedPoints > 0 ? songEstimatedPoints : Math.Max(songWrittenPoints, 1);
        long safeTopTotal = totalSongs > 0 ? totalSongs : 1;
        const long topScale = 1000;
        long safeTopTotalScaled = checked(safeTopTotal * topScale);
        long safeTopProcessedScaled = totalSongs > 0 ? checked(completedSongCount * topScale) : topScale;

        double songRatio = BatchExecutionSupport.ToRatio(songWrittenPoints, safeSongTotal);
        if (totalSongs > 0 && completedSongCount < safeTopTotal)
        {
            safeTopProcessedScaled = Math.Min(
                safeTopTotalScaled,
                checked(safeTopProcessedScaled + (long)Math.Round(songRatio * topScale, MidpointRounding.AwayFromZero)));
        }

        string topLabel = string.Create(
            CultureInfo.InvariantCulture,
            $"Songs {Math.Min(completedSongCount, safeTopTotal)}/{safeTopTotal} [{songName}]");
        string bottomLabel = string.Create(
            CultureInfo.InvariantCulture,
            $"{songName} points {Math.Min(songWrittenPoints, safeSongTotal)}/{safeSongTotal}");

        BatchExecutionSupport.ReportDualProgress(
            progressDisplay,
            topLabel,
            safeTopProcessedScaled,
            safeTopTotalScaled,
            bottomLabel,
            songWrittenPoints,
            safeSongTotal);
    }

    private sealed class SongBatch
    {
        public SongBatch(string name, IReadOnlyList<StemAudioFile> targets, long estimatedPointCount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(targets);
            ArgumentOutOfRangeException.ThrowIfNegative(estimatedPointCount);

            Name = name;
            Targets = targets;
            EstimatedPointCount = estimatedPointCount;
        }

        public string Name { get; }

        public IReadOnlyList<StemAudioFile> Targets { get; }

        public long EstimatedPointCount { get; }
    }

    private sealed class SongBatchBuilder
    {
        public SongBatchBuilder(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Name = name;
        }

        public string Name { get; }

        public List<StemAudioFile> Targets { get; } = new();

        public long EstimatedPointCount { get; set; }
    }

    private sealed class ProgressReportingPeakWriter : IPeakAnalysisPointWriter
    {
        private readonly IPeakAnalysisPointWriter inner;
        private readonly Action<int> onWrite;

        public ProgressReportingPeakWriter(IPeakAnalysisPointWriter inner, Action<int> onWrite)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.onWrite = onWrite ?? throw new ArgumentNullException(nameof(onWrite));
        }

        public void Write(PeakAnalysisPoint point)
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
