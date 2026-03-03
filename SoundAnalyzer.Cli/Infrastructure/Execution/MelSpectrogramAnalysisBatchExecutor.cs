using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using Cli.Shared.Application.Ports;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Application.UseCases;
using MelSpectrogramAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Progress;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class MelSpectrogramAnalysisBatchExecutor
{
    private readonly MelSpectrogramAnalysisUseCase melSpectrogramAnalysisUseCase;
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly ITextBlockProgressDisplayFactory progressDisplayFactory;
    private readonly IAnalysisStoreFactory analysisStoreFactory;

    public MelSpectrogramAnalysisBatchExecutor(
        MelSpectrogramAnalysisUseCase melSpectrogramAnalysisUseCase,
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        ITextBlockProgressDisplayFactory progressDisplayFactory,
        IAnalysisStoreFactory analysisStoreFactory)
    {
        this.melSpectrogramAnalysisUseCase = melSpectrogramAnalysisUseCase ?? throw new ArgumentNullException(nameof(melSpectrogramAnalysisUseCase));
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.progressDisplayFactory = progressDisplayFactory ?? throw new ArgumentNullException(nameof(progressDisplayFactory));
        this.analysisStoreFactory = analysisStoreFactory ?? throw new ArgumentNullException(nameof(analysisStoreFactory));
    }

#pragma warning disable CA1031
    public async Task<MelSpectrogramAnalysisExecutionResult> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        int melBinCount = arguments.MelBinCount ?? throw new CliException(CliErrorCode.UnsupportedMode, arguments.Mode);
        string anchorColumnName = arguments.HopUnit == AnalysisLengthUnit.Sample ? "sample" : "ms";

        if (arguments.StorageBackend == StorageBackend.Sqlite)
        {
            BatchExecutionSupport.EnsureDbDirectory(
                arguments.DbFilePath ?? throw new CliException(CliErrorCode.DbFileRequired, "SQLite mode requires db file path."));
        }
        ResolvedMelSpectrogramAudioFiles resolved = MelSpectrogramAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Recursive);

        using SoundAnalyzerProgressTracker progressTracker = SoundAnalyzerProgressTracker.Create(
            arguments.ShowProgress,
            progressDisplayFactory);
        progressTracker.Configure(
            resolved.Files.Select(file => file.Name).ToArray(),
            arguments.MelFileThreads,
            arguments.InsertQueueSize);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);
        using IMelSpectrogramAnalysisStore store = analysisStoreFactory.CreateMelSpectrogramStore(
            arguments,
            anchorColumnName,
            melBinCount,
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

        Channel<QueuedMelSpectrogramPoint> insertChannel = Channel.CreateBounded<QueuedMelSpectrogramPoint>(insertChannelOptions);
        Exception? consumerException = null;
        long insertedPointCount = 0;
        ConcurrentDictionary<string, byte> warningSet = new(StringComparer.Ordinal);

        Task consumerTask = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (QueuedMelSpectrogramPoint queuedPoint in insertChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
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

        QueuedMelSpectrogramWriter queuedWriter = new(
            insertChannel.Writer,
            progressTracker,
            () => consumerException,
            executionToken);

        Channel<MelSpectrogramAudioFile> songChannel = Channel.CreateUnbounded<MelSpectrogramAudioFile>(
            new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
            });

        for (int i = 0; i < resolved.Files.Count; i++)
        {
            if (!songChannel.Writer.TryWrite(resolved.Files[i]))
            {
                throw new InvalidOperationException("Failed to enqueue Mel spectrogram song job.");
            }
        }

        songChannel.Writer.TryComplete();

        Task[] workerTasks = new Task[arguments.MelFileThreads];
        for (int workerId = 0; workerId < workerTasks.Length; workerId++)
        {
            int capturedWorkerId = workerId;
            workerTasks[workerId] = Task.Run(
                async () =>
                {
                    await foreach (MelSpectrogramAudioFile target in songChannel.Reader.ReadAllAsync(executionToken).ConfigureAwait(false))
                    {
                        progressTracker.SetWorkerSong(capturedWorkerId, target.Name);
                        try
                        {
                            await ProcessFileAsync(
                                    target,
                                    arguments,
                                    melBinCount,
                                    toolPaths,
                                    progressTracker,
                                    queuedWriter,
                                    warningSet,
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

        MelSpectrogramAnalysisBatchSummary summary = new(
            resolved.DirectoryCount,
            resolved.Files.Count,
            insertedPointCount,
            arguments.TableName,
            melBinCount);

        string[] warnings = warningSet.Keys
            .OrderBy(warning => warning, StringComparer.Ordinal)
            .ToArray();
        return new MelSpectrogramAnalysisExecutionResult(summary, warnings);
    }
#pragma warning restore CA1031

    private async Task ProcessFileAsync(
        MelSpectrogramAudioFile target,
        CommandLineArguments arguments,
        int melBinCount,
        FfmpegToolPaths toolPaths,
        SoundAnalyzerProgressTracker progressTracker,
        IMelSpectrogramAnalysisPointWriter queuedWriter,
        ConcurrentDictionary<string, byte> warningSet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentNullException.ThrowIfNull(progressTracker);
        ArgumentNullException.ThrowIfNull(queuedWriter);
        ArgumentNullException.ThrowIfNull(warningSet);

        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, target.FilePath, cancellationToken)
            .ConfigureAwait(false);

        int analysisSampleRate = arguments.TargetSamplingHz ?? streamInfo.SampleRate;
        long windowSamples = ResolveSamples(arguments.WindowValue, arguments.WindowUnit, analysisSampleRate);
        long hopSamples = ResolveSamples(arguments.HopValue, arguments.HopUnit, analysisSampleRate);
        double nyquist = analysisSampleRate / 2.0;
        double melFmaxHz = arguments.MelFmaxHz > nyquist ? nyquist : arguments.MelFmaxHz;

        if (arguments.MelFmaxHz > nyquist)
        {
            string warningDetail = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"name={target.Name}, requested={arguments.MelFmaxHz}, nyquist={nyquist}, applied={melFmaxHz}");
            string warning = ConsoleTexts.WithValue(ConsoleTexts.MelFmaxClampedWarningPrefix, warningDetail);
            _ = warningSet.TryAdd(warning, 0);
        }

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

        MelSpectrogramAnalysisRequest request = new(
            target.FilePath,
            target.Name,
            windowSamples,
            hopSamples,
            analysisSampleRate,
            arguments.HopUnit == AnalysisLengthUnit.Sample ? MelSpectrogramAnchorUnit.Sample : MelSpectrogramAnchorUnit.Millisecond,
            arguments.WindowValue,
            melBinCount,
            arguments.MelFminHz,
            melFmaxHz,
            MapMelScale(arguments.MelScale),
            arguments.MelPower,
            arguments.MinLimitDb,
            arguments.FfmpegPath,
            arguments.MelProcThreads);

        _ = await melSpectrogramAnalysisUseCase
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

    private static MelSpectrogramScaleKind MapMelScale(MelScaleOption melScale)
    {
        return melScale switch
        {
            MelScaleOption.Slaney => MelSpectrogramScaleKind.Slaney,
            MelScaleOption.Htk => MelSpectrogramScaleKind.Htk,
            _ => throw new CliException(CliErrorCode.None, $"Unsupported mel-scale option: {melScale}"),
        };
    }

    private sealed class QueuedMelSpectrogramWriter : IMelSpectrogramAnalysisPointWriter
    {
        private readonly ChannelWriter<QueuedMelSpectrogramPoint> writer;
        private readonly SoundAnalyzerProgressTracker progressTracker;
        private readonly CancellationToken cancellationToken;
        private readonly Func<Exception?> consumerExceptionProvider;

        public QueuedMelSpectrogramWriter(
            ChannelWriter<QueuedMelSpectrogramPoint> writer,
            SoundAnalyzerProgressTracker progressTracker,
            Func<Exception?> consumerExceptionProvider,
            CancellationToken cancellationToken)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
            this.cancellationToken = cancellationToken;
            this.consumerExceptionProvider = consumerExceptionProvider ?? throw new ArgumentNullException(nameof(consumerExceptionProvider));
        }

        public void Write(MelSpectrogramAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);

            Exception? consumerException = consumerExceptionProvider();
            if (consumerException is not null)
            {
                throw new CliException(CliErrorCode.None, "Insert pipeline failed.", consumerException);
            }

            writer.WriteAsync(new QueuedMelSpectrogramPoint(point.Name, point), cancellationToken).AsTask().GetAwaiter().GetResult();
            progressTracker.IncrementEnqueued(point.Name);
        }
    }

    private readonly record struct QueuedMelSpectrogramPoint(string SongName, MelSpectrogramAnalysisPoint Point);
}

