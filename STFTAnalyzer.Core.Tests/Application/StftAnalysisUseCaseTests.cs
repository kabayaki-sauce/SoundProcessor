using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using STFTAnalyzer.Core.Application.Errors;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Application.UseCases;
using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Tests.Application;

public sealed class StftAnalysisUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStartAnchorsAtHopSamples_WhenAnchorUnitIsSample()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 100, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 50,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 50,
                binCount: 12,
                minLimitDb: -120,
                ffmpegPath: null);

            StftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 50,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Millisecond,
                windowPersistedValue: 20,
                binCount: 8,
                minLimitDb: -120,
                ffmpegPath: null);

            StftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 2, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 20,
                hopSamples: 20,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 20,
                binCount: 4,
                minLimitDb: -120,
                ffmpegPath: null,
                processingThreads: 2);

            StftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 30,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 30,
                binCount: 8,
                minLimitDb: -120,
                ffmpegPath: null,
                processingThreads: 4);

            StftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 20,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 20,
                binCount: 8,
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
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 48_000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 2048,
                hopSamples: 512,
                analysisSampleRate: 44_100,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 2048,
                binCount: 12,
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
    public async Task ExecuteAsync_ShouldRejectBinCount_WhenExceedsPositiveFrequencyBinCount()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 200, value: 0.2F);
            CapturingPcmFrameReader pcmFrameReader = new(frames);
            StftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, pcmFrameReader);
            CollectingWriter writer = new();

            StftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSamples: 100,
                hopSamples: 10,
                analysisSampleRate: 1000,
                anchorUnit: StftAnchorUnit.Sample,
                windowPersistedValue: 100,
                binCount: 70,
                minLimitDb: -120,
                ffmpegPath: null);

            StftAnalysisException exception = await Assert.ThrowsAsync<StftAnalysisException>(
                () => useCase.ExecuteAsync(request, writer, CancellationToken.None));

            Assert.Equal(StftAnalysisErrorCode.InvalidBinCount, exception.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);
        }
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

    private static StftAnalysisUseCase BuildUseCase(int sampleRate, int channels, CapturingPcmFrameReader pcmFrameReader)
    {
        AudioStreamInfo streamInfo = new(sampleRate, channels, AudioPcmBitDepth.F32, pcmFrameReader.FrameCount);
        return new StftAnalysisUseCase(
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

    private sealed class CollectingWriter : IStftAnalysisPointWriter
    {
        private readonly object sync = new();

        public List<StftAnalysisPoint> Points { get; } = new();

        public void Write(StftAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            lock (sync)
            {
                Points.Add(point);
            }
        }
    }
}
