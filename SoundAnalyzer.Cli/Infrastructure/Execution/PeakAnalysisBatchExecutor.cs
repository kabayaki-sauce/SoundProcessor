using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Ports;
using PeakAnalyzer.Core.Application.Models;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Application.UseCases;
using PeakAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Progress;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class PeakAnalysisBatchExecutor
{
    private readonly PeakAnalysisUseCase peakAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly ITextBlockProgressDisplayFactory progressDisplayFactory;

    public PeakAnalysisBatchExecutor(
        PeakAnalysisUseCase peakAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        ITextBlockProgressDisplayFactory progressDisplayFactory)
    {
        this.peakAnalysisUseCase = peakAnalysisUseCase ?? throw new ArgumentNullException(nameof(peakAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.progressDisplayFactory = progressDisplayFactory ?? throw new ArgumentNullException(nameof(progressDisplayFactory));
    }

#pragma warning disable CA1031
    public async Task<PeakAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        BatchExecutionSupport.EnsureDbDirectory(arguments.DbFilePath);

        ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Stems);
        List<SongBatch> songs = BuildSongBatches(resolved.Files);
        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);

        using SoundAnalyzerProgressTracker progressTracker = SoundAnalyzerProgressTracker.Create(
            arguments.ShowProgress,
            progressDisplayFactory);
        progressTracker.Configure(
            songs.Select(song => song.Name).ToArray(),
            arguments.PeakFileThreads,
            arguments.InsertQueueSize);

        using SqlitePeakAnalysisStore store = new(arguments.DbFilePath, arguments.TableName, conflictMode);
        store.Initialize();

        FfmpegToolPaths? progressProbeToolPaths = null;
        if (arguments.ShowProgress)
        {
            progressProbeToolPaths = ffmpegLocator.Resolve(arguments.FfmpegPath);
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken executionToken = linkedCts.Token;

        BoundedChannelOptions insertChannelOptions = new(arguments.InsertQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        };

        Channel<QueuedPeakPoint> insertChannel = Channel.CreateBounded<QueuedPeakPoint>(insertChannelOptions);
        Exception? consumerException = null;
        long insertedPointCount = 0;

        Task consumerTask = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (QueuedPeakPoint queuedPoint in insertChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
                    {
                        store.Write(queuedPoint.Point);
                        insertedPointCount++;
                        progressTracker.IncrementInserted(queuedPoint.SongName);
                    }
                }
                catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    consumerException = ex;
                    await linkedCts.CancelAsync().ConfigureAwait(false);
                }
            },
            CancellationToken.None);

        QueuedPeakWriter queuedWriter = new(
            insertChannel.Writer,
            progressTracker,
            () => consumerException,
            executionToken);

        Channel<SongBatch> songChannel = Channel.CreateUnbounded<SongBatch>(
            new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
            });

        for (int i = 0; i < songs.Count; i++)
        {
            if (!songChannel.Writer.TryWrite(songs[i]))
            {
                throw new InvalidOperationException("Failed to enqueue song job.");
            }
        }

        songChannel.Writer.TryComplete();

        Task[] workerTasks = new Task[arguments.PeakFileThreads];
        for (int workerId = 0; workerId < workerTasks.Length; workerId++)
        {
            int capturedWorkerId = workerId;
            workerTasks[workerId] = Task.Run(
                async () =>
                {
                    await foreach (SongBatch song in songChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
                    {
                        progressTracker.SetWorkerSong(capturedWorkerId, song.Name);
                        try
                        {
                            if (progressProbeToolPaths is not null)
                            {
                                await ConfigureExpectedPointsForSongAsync(
                                        song,
                                        progressProbeToolPaths,
                                        arguments.HopMs,
                                        progressTracker,
                                        executionToken)
                                    .ConfigureAwait(false);
                            }

                            await ProcessSongAsync(song, arguments, queuedWriter, executionToken).ConfigureAwait(false);
                            progressTracker.MarkWorkerAnalyzeCompleted(capturedWorkerId);
                        }
                        finally
                        {
                            progressTracker.SetWorkerIdle(capturedWorkerId);
                        }
                    }
                },
                CancellationToken.None);
        }

        Exception? producerException = null;
        try
        {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            producerException = ex;
            await linkedCts.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            insertChannel.Writer.TryComplete(producerException);
        }

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
        {
            throw;
        }

        if (consumerException is not null)
        {
            throw new CliException(CliErrorCode.None, "Insert pipeline failed.", consumerException);
        }

        if (producerException is not null)
        {
            ExceptionDispatchInfo.Capture(producerException).Throw();
        }

        store.Complete();

        return new PeakAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            resolved.SkippedStemCount,
            insertedPointCount,
            arguments.TableName);
    }
#pragma warning restore CA1031

    private async Task ProcessSongAsync(
        SongBatch song,
        CommandLineArguments arguments,
        IPeakAnalysisPointWriter queuedWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(queuedWriter);

        ParallelOptions options = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = arguments.PeakProcThreads,
        };

        await Parallel.ForEachAsync(
                song.Targets,
                options,
                async (target, token) =>
                {
                    PeakAnalysisRequest request = new(
                        target.FilePath,
                        target.Name,
                        target.Stem,
                        arguments.WindowSizeMs,
                        arguments.HopMs,
                        arguments.MinLimitDb,
                        arguments.FfmpegPath);

                    _ = await peakAnalysisUseCase
                        .ExecuteAsync(request, queuedWriter, token)
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    private async Task ConfigureExpectedPointsForSongAsync(
        SongBatch song,
        FfmpegToolPaths toolPaths,
        long hopMs,
        SoundAnalyzerProgressTracker progressTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);
        ArgumentNullException.ThrowIfNull(progressTracker);

        long expectedPointCount = 0;
        for (int i = 0; i < song.Targets.Count; i++)
        {
            AudioStreamInfo streamInfo = await audioProbeService
                .ProbeAsync(toolPaths, song.Targets[i].FilePath, cancellationToken)
                .ConfigureAwait(false);

            bool estimated = BatchExecutionSupport.TryEstimatePeakPointCountPerTarget(
                streamInfo,
                hopMs,
                out long targetExpectedPointCount);
            if (!estimated)
            {
                progressTracker.MarkSongExpectedPointsUnknown(song.Name);
                return;
            }

            expectedPointCount = checked(expectedPointCount + targetExpectedPointCount);
        }

        progressTracker.SetSongExpectedPoints(song.Name, expectedPointCount);
    }

    private static List<SongBatch> BuildSongBatches(IReadOnlyList<StemAudioFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        Dictionary<string, List<StemAudioFile>> groupedTargets = new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedSongNames = new();

        for (int i = 0; i < files.Count; i++)
        {
            StemAudioFile file = files[i];
            if (!groupedTargets.TryGetValue(file.Name, out List<StemAudioFile>? targets))
            {
                targets = new List<StemAudioFile>();
                groupedTargets[file.Name] = targets;
                orderedSongNames.Add(file.Name);
            }

            targets.Add(file);
        }

        List<SongBatch> songs = new(orderedSongNames.Count);
        for (int i = 0; i < orderedSongNames.Count; i++)
        {
            string songName = orderedSongNames[i];
            songs.Add(new SongBatch(songName, groupedTargets[songName]));
        }

        return songs;
    }

    private sealed class QueuedPeakWriter : IPeakAnalysisPointWriter
    {
        private readonly ChannelWriter<QueuedPeakPoint> writer;
        private readonly SoundAnalyzerProgressTracker progressTracker;
        private readonly CancellationToken cancellationToken;
        private readonly Func<Exception?> consumerExceptionProvider;

        public QueuedPeakWriter(
            ChannelWriter<QueuedPeakPoint> writer,
            SoundAnalyzerProgressTracker progressTracker,
            Func<Exception?> consumerExceptionProvider,
            CancellationToken cancellationToken)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
            this.cancellationToken = cancellationToken;
            this.consumerExceptionProvider = consumerExceptionProvider ?? throw new ArgumentNullException(nameof(consumerExceptionProvider));
        }

        public void Write(PeakAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);

            Exception? consumerException = consumerExceptionProvider();
            if (consumerException is not null)
            {
                throw new CliException(CliErrorCode.None, "Insert pipeline failed.", consumerException);
            }

            writer.WriteAsync(new QueuedPeakPoint(point.Name, point), cancellationToken).AsTask().GetAwaiter().GetResult();
            progressTracker.IncrementEnqueued(point.Name);
        }
    }

    private sealed class SongBatch
    {
        public SongBatch(string name, IReadOnlyList<StemAudioFile> targets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(targets);

            Name = name;
            Targets = targets;
        }

        public string Name { get; }

        public IReadOnlyList<StemAudioFile> Targets { get; }
    }

    private readonly record struct QueuedPeakPoint(string SongName, PeakAnalysisPoint Point);
}
