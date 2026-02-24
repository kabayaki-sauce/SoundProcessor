using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Ports;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Application.UseCases;
using STFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Progress;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class StftAnalysisBatchExecutor
{
    private readonly StftAnalysisUseCase stftAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly ITextBlockProgressDisplayFactory progressDisplayFactory;
    private readonly IAnalysisStoreFactory analysisStoreFactory;

    public StftAnalysisBatchExecutor(
        StftAnalysisUseCase stftAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        ITextBlockProgressDisplayFactory progressDisplayFactory,
        IAnalysisStoreFactory analysisStoreFactory)
    {
        this.stftAnalysisUseCase = stftAnalysisUseCase ?? throw new ArgumentNullException(nameof(stftAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.progressDisplayFactory = progressDisplayFactory ?? throw new ArgumentNullException(nameof(progressDisplayFactory));
        this.analysisStoreFactory = analysisStoreFactory ?? throw new ArgumentNullException(nameof(analysisStoreFactory));
    }

#pragma warning disable CA1031
    public async Task<StftAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        int binCount = arguments.BinCount ?? throw new CliException(CliErrorCode.UnsupportedMode, arguments.Mode);
        string anchorColumnName = arguments.HopUnit == AnalysisLengthUnit.Sample ? "sample" : "ms";

        if (arguments.StorageBackend == StorageBackend.Sqlite)
        {
            BatchExecutionSupport.EnsureDbDirectory(
                arguments.DbFilePath ?? throw new CliException(CliErrorCode.DbFileRequired, "SQLite mode requires db file path."));
        }
        ResolvedStftAudioFiles resolved = StftAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Recursive);

        using SoundAnalyzerProgressTracker progressTracker = SoundAnalyzerProgressTracker.Create(
            arguments.ShowProgress,
            progressDisplayFactory);
        progressTracker.Configure(
            resolved.Files.Select(file => file.Name).ToArray(),
            arguments.StftFileThreads,
            arguments.InsertQueueSize);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);
        using IStftAnalysisStore store = analysisStoreFactory.CreateStftStore(
            arguments,
            anchorColumnName,
            binCount,
            conflictMode);
        store.Initialize();

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(arguments.FfmpegPath);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken executionToken = linkedCts.Token;

        BoundedChannelOptions insertChannelOptions = new(arguments.InsertQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        };

        Channel<QueuedStftPoint> insertChannel = Channel.CreateBounded<QueuedStftPoint>(insertChannelOptions);
        Exception? consumerException = null;
        long insertedPointCount = 0;

        Task consumerTask = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (QueuedStftPoint queuedPoint in insertChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
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

        QueuedStftWriter queuedWriter = new(
            insertChannel.Writer,
            progressTracker,
            () => consumerException,
            executionToken);

        Channel<StftAudioFile> songChannel = Channel.CreateUnbounded<StftAudioFile>(
            new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
            });

        for (int i = 0; i < resolved.Files.Count; i++)
        {
            if (!songChannel.Writer.TryWrite(resolved.Files[i]))
            {
                throw new InvalidOperationException("Failed to enqueue STFT song job.");
            }
        }

        songChannel.Writer.TryComplete();

        Task[] workerTasks = new Task[arguments.StftFileThreads];
        for (int workerId = 0; workerId < workerTasks.Length; workerId++)
        {
            int capturedWorkerId = workerId;
            workerTasks[workerId] = Task.Run(
                async () =>
                {
                    await foreach (StftAudioFile target in songChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
                    {
                        progressTracker.SetWorkerSong(capturedWorkerId, target.Name);
                        try
                        {
                            await ProcessFileAsync(
                                    target,
                                    arguments,
                                    binCount,
                                    toolPaths,
                                    progressTracker,
                                    queuedWriter,
                                    executionToken)
                                .ConfigureAwait(false);
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

        return new StftAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            insertedPointCount,
            arguments.TableName,
            binCount);
    }
#pragma warning restore CA1031

    private async Task ProcessFileAsync(
        StftAudioFile target,
        CommandLineArguments arguments,
        int binCount,
        FfmpegToolPaths toolPaths,
        SoundAnalyzerProgressTracker progressTracker,
        IStftAnalysisPointWriter queuedWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentNullException.ThrowIfNull(progressTracker);
        ArgumentNullException.ThrowIfNull(queuedWriter);

        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, target.FilePath, cancellationToken)
            .ConfigureAwait(false);

        int analysisSampleRate = arguments.TargetSamplingHz ?? streamInfo.SampleRate;
        long windowSamples = ResolveSamples(arguments.WindowValue, arguments.WindowUnit, analysisSampleRate);
        long hopSamples = ResolveSamples(arguments.HopValue, arguments.HopUnit, analysisSampleRate);

        bool estimated = BatchExecutionSupport.TryEstimateStftPointCountPerFile(
            streamInfo,
            analysisSampleRate,
            hopSamples,
            out long expectedPointCount);
        if (estimated)
        {
            progressTracker.SetSongExpectedPoints(target.Name, expectedPointCount);
        }
        else
        {
            progressTracker.MarkSongExpectedPointsUnknown(target.Name);
        }

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
            arguments.FfmpegPath,
            arguments.StftProcThreads);

        _ = await stftAnalysisUseCase
            .ExecuteAsync(request, queuedWriter, cancellationToken)
            .ConfigureAwait(false);
    }

    private static long ResolveSamples(long value, AnalysisLengthUnit unit, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        return unit == AnalysisLengthUnit.Sample
            ? value
            : BatchExecutionSupport.ConvertDurationMsToSamples(value, sampleRate);
    }

    private sealed class QueuedStftWriter : IStftAnalysisPointWriter
    {
        private readonly ChannelWriter<QueuedStftPoint> writer;
        private readonly SoundAnalyzerProgressTracker progressTracker;
        private readonly CancellationToken cancellationToken;
        private readonly Func<Exception?> consumerExceptionProvider;

        public QueuedStftWriter(
            ChannelWriter<QueuedStftPoint> writer,
            SoundAnalyzerProgressTracker progressTracker,
            Func<Exception?> consumerExceptionProvider,
            CancellationToken cancellationToken)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
            this.cancellationToken = cancellationToken;
            this.consumerExceptionProvider = consumerExceptionProvider ?? throw new ArgumentNullException(nameof(consumerExceptionProvider));
        }

        public void Write(StftAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);

            Exception? consumerException = consumerExceptionProvider();
            if (consumerException is not null)
            {
                throw new CliException(CliErrorCode.None, "Insert pipeline failed.", consumerException);
            }

            writer.WriteAsync(new QueuedStftPoint(point.Name, point), cancellationToken).AsTask().GetAwaiter().GetResult();
            progressTracker.IncrementEnqueued(point.Name);
        }
    }

    private readonly record struct QueuedStftPoint(string SongName, StftAnalysisPoint Point);
}
