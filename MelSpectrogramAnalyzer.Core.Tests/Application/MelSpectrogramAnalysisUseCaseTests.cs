using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using MelSpectrogramAnalyzer.Core.Application.Errors;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Application.UseCases;
using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Tests.Application;

public sealed class MelSpectrogramAnalysisUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStartAnchorsAtHopSamples_WhenAnchorUnitIsSample()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 100, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 50,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 50,
                melBinCount: 12,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(10, writer.Points[0].Anchor);
            Assert.DoesNotContain(writer.Points, point => point.Anchor == 0);
            Assert.Equal(10, summary.PointCount);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitMillisecondAnchors_WhenAnchorUnitIsMillisecond()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 100, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 50,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Millisecond,
                windowPersistedValue: 20,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(10, writer.Points[0].Anchor);
            Assert.Equal(20, writer.Points[0].Window);
            Assert.Equal(100, summary.LastAnchor);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitRowsPerChannel()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 2, count: 20, value: 0.0F);
            frames[10][0] = 0.5F;
            frames[10][1] = 0.1F;

            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 2, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 20,
                hopSamples: 20,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 20,
                melBinCount: 4,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null,
                processingThreads: 2);

            MelSpectrogramAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(2, summary.PointCount);
            Assert.Equal(2, writer.Points.Count);
            Assert.Contains(writer.Points, point => point.Channel == 0);
            Assert.Contains(writer.Points, point => point.Channel == 1);
            Assert.All(writer.Points, point => Assert.Equal(4, point.Bins.Count));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportWindowPipelineMode_WhenProcessingThreadsExceedChannels()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 120, value: 0.3F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 30,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 30,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null,
                processingThreads: 4);

            MelSpectrogramAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(12, summary.PointCount);
            Assert.Equal(12, writer.Points.Count);
            Assert.All(writer.Points, point => Assert.Equal(8, point.Bins.Count));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClampBinsByMinLimit()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 20, value: 0.0F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 20,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 20,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -40,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.All(
                writer.Points,
                point => Assert.All(point.Bins, value => Assert.Equal(-40, value, precision: 6)));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassTargetSamplingToReader_WhenDifferentFromInput()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 200, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 48_000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 2048,
                hopSamples: 512,
                analysisSampleRate: 44_100,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 2048,
                melBinCount: 12,
                melFminHz: 20,
                melFmaxHz: 20_000,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(44_100, pcmFrameReader.LastTargetSampleRateHz);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportMelScaleKindSwitch()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 256, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 2000, channels: 1, pcmFrameReader);
            CollectingWriter slaneyWriter = new();
            CollectingWriter htkWriter = new();

            MelSpectrogramAnalysisRequest slaneyRequest = new(
                inputPath,
                "song",
                windowSamples: 128,
                hopSamples: 64,
                analysisSampleRate: 2000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 128,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 900,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisRequest htkRequest = new(
                inputPath,
                "song",
                windowSamples: 128,
                hopSamples: 64,
                analysisSampleRate: 2000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 128,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 900,
                melScaleKind: MelSpectrogramScaleKind.Htk,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(slaneyRequest, slaneyWriter, CancellationToken.None);
            _ = await useCase.ExecuteAsync(htkRequest, htkWriter, CancellationToken.None);

            Assert.NotEmpty(slaneyWriter.Points);
            Assert.NotEmpty(htkWriter.Points);
            Assert.NotEqual(slaneyWriter.Points[0].Bins[0], htkWriter.Points[0].Bins[0], precision: 6);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportMelPowerSwitch()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 256, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 2000, channels: 1, pcmFrameReader);
            CollectingWriter magnitudeWriter = new();
            CollectingWriter powerWriter = new();

            MelSpectrogramAnalysisRequest magnitudeRequest = new(
                inputPath,
                "song",
                windowSamples: 128,
                hopSamples: 64,
                analysisSampleRate: 2000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 128,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 900,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 1,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisRequest powerRequest = new(
                inputPath,
                "song",
                windowSamples: 128,
                hopSamples: 64,
                analysisSampleRate: 2000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 128,
                melBinCount: 8,
                melFminHz: 20,
                melFmaxHz: 900,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(magnitudeRequest, magnitudeWriter, CancellationToken.None);
            _ = await useCase.ExecuteAsync(powerRequest, powerWriter, CancellationToken.None);

            Assert.NotEmpty(magnitudeWriter.Points);
            Assert.NotEmpty(powerWriter.Points);
            Assert.NotEqual(magnitudeWriter.Points[0].Bins[0], powerWriter.Points[0].Bins[0], precision: 6);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectBinCount_WhenExceedsPositiveFrequencyBinCount()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 200, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 100,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 100,
                melBinCount: 70,
                melFminHz: 20,
                melFmaxHz: 450,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisException exception = await Assert.ThrowsAsync<MelSpectrogramAnalysisException>(
                () => useCase.ExecuteAsync(request, writer, CancellationToken.None));

            Assert.Equal(MelSpectrogramAnalysisErrorCode.InvalidMelBinCount, exception.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectMelFrequencies_WhenFmaxExceedsNyquist()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 200, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            MelSpectrogramAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            MelSpectrogramAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 100,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 100,
                melBinCount: 32,
                melFminHz: 20,
                melFmaxHz: 600,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 2,
                minLimitDb: -120,
                ffmpegPath: null);

            MelSpectrogramAnalysisException exception = await Assert.ThrowsAsync<MelSpectrogramAnalysisException>(
                () => useCase.ExecuteAsync(request, writer, CancellationToken.None));

            Assert.Equal(MelSpectrogramAnalysisErrorCode.InvalidMelFrequencies, exception.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public void Request_ShouldRejectMelPower_OtherThan1Or2()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MelSpectrogramAnalysisRequest(
                inputFilePath: "a.wav",
                name: "song",
                windowSamples: 100,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: MelSpectrogramAnchorUnit.Sample,
                windowPersistedValue: 100,
                melBinCount: 32,
                melFminHz: 20,
                melFmaxHz: 400,
                melScaleKind: MelSpectrogramScaleKind.Slaney,
                melPower: 3,
                minLimitDb: -120,
                ffmpegPath: null));
    }

    private static float[][] CreateFrames(int channels, int count, float value)
    {
        float[][] frames = new float[count][];
        for (int i = 0; i < count; i++)
        {
            float[] frame = new float[channels];
            for (int channel = 0; channel < channels; channel++)
            {
                frame[channel] = value;
            }

            frames[i] = frame;
        }

        return frames;
    }

    private static MelSpectrogramAnalysisUseCase BuildUseCase(int sampleRate, int channels, CapturingPcmFrameReader pcmFrameReader)
    {
        AudioStreamInfo streamInfo = new(sampleRate, channels, AudioPcmBitDepth.F32, pcmFrameReader.FrameCount);
        return new MelSpectrogramAnalysisUseCase(
            new FakeFfmpegLocator(),
            new FakeProbeService(streamInfo),
            pcmFrameReader);
    }

    private static string CreateTempInputFile()
    {
        string path = Path.GetTempFileName();
        using FileStream stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        stream.WriteByte(0);
        return path;
    }

    private sealed class FakeFfmpegLocator : IFfmpegLocator
    {
        public FfmpegToolPaths Resolve(string? ffmpegPath)
        {
            return new FfmpegToolPaths("ffmpeg", "ffprobe");
        }
    }

    private sealed class FakeProbeService : IAudioProbeService
    {
        private readonly AudioStreamInfo streamInfo;

        public FakeProbeService(AudioStreamInfo streamInfo)
        {
            this.streamInfo = streamInfo ?? throw new ArgumentNullException(nameof(streamInfo));
        }

        public Task<AudioStreamInfo> ProbeAsync(
            FfmpegToolPaths toolPaths,
            string inputFilePath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(streamInfo);
        }
    }

    private sealed class CapturingPcmFrameReader : IAudioPcmFrameReader
    {
        private readonly IReadOnlyList<float[]> frames;

        public CapturingPcmFrameReader(IReadOnlyList<float[]> frames)
        {
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        }

        public int? LastTargetSampleRateHz { get; private set; }

        public int FrameCount => frames.Count;

        public Task ReadFramesAsync(
            FfmpegToolPaths toolPaths,
            string inputFilePath,
            int channels,
            IAudioPcmFrameSink frameSink,
            CancellationToken cancellationToken,
            int? targetSampleRateHz = null)
        {
            ArgumentNullException.ThrowIfNull(frameSink);
            LastTargetSampleRateHz = targetSampleRateHz;

            for (int i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float[] frame = frames[i];
                Assert.Equal(channels, frame.Length);
                frameSink.OnFrame(frame);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CollectingWriter : IMelSpectrogramAnalysisPointWriter
    {
        private readonly object sync = new();

        public List<MelSpectrogramAnalysisPoint> Points { get; } = new();

        public void Write(MelSpectrogramAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            lock (sync)
            {
                Points.Add(point);
            }
        }
    }
}

