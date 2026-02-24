using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using SFFTAnalyzer.Core.Application.Models;
using SFFTAnalyzer.Core.Application.Ports;
using SFFTAnalyzer.Core.Application.UseCases;
using SFFTAnalyzer.Core.Domain.Models;

namespace SFFTAnalyzer.Core.Tests.Application;

public sealed class SfftAnalysisUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStartAnchorsAtHopMs()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 100, value: 0.2F);
            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 50,
                hopMs: 10,
                binCount: 12,
                minLimitDb: -120,
                ffmpegPath: null);

            SfftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(10, writer.Points[0].Ms);
            Assert.DoesNotContain(writer.Points, point => point.Ms == 0);
            Assert.Equal(10, summary.PointCount);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitForEarlyAnchorsByPaddingWithSilence()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 10, value: 0.0F);
            frames[0][0] = 1.0F;

            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 50,
                hopMs: 10,
                binCount: 8,
                minLimitDb: -120,
                ffmpegPath: null);

            SfftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(1, summary.PointCount);
            Assert.Single(writer.Points);
            Assert.Equal(10, writer.Points[0].Ms);
            Assert.Equal(8, writer.Points[0].Bins.Count);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDropTailRemainderFrames()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 10_233, value: 0.0F);
            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 50,
                hopMs: 10,
                binCount: 6,
                minLimitDb: -120,
                ffmpegPath: null);

            SfftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(1023, summary.PointCount);
            Assert.Equal(10_230, summary.LastMs);
            Assert.Equal(10_230, writer.Points[^1].Ms);
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

            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 2, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 20,
                hopMs: 20,
                binCount: 4,
                minLimitDb: -120,
                ffmpegPath: null);

            SfftAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
    public async Task ExecuteAsync_ShouldEmitConfiguredBinCount()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateFrames(channels: 1, count: 50, value: 0.2F);
            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 25,
                hopMs: 10,
                binCount: 12,
                minLimitDb: -120,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.All(writer.Points, point => Assert.Equal(12, point.Bins.Count));
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
            SfftAnalysisUseCase useCase = BuildUseCase(sampleRate: 1000, channels: 1, frames);
            CollectingWriter writer = new();

            SfftAnalysisRequest request = new(
                inputPath,
                "song",
                windowSizeMs: 20,
                hopMs: 10,
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

    private static SfftAnalysisUseCase BuildUseCase(int sampleRate, int channels, float[][] frames)
    {
        AudioStreamInfo streamInfo = new(sampleRate, channels, AudioPcmBitDepth.F32, frames.Length);
        return new SfftAnalysisUseCase(
            new FakeFfmpegLocator(),
            new FakeProbeService(streamInfo),
            new FakePcmFrameReader(frames));
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

    private sealed class FakePcmFrameReader : IAudioPcmFrameReader
    {
        private readonly IReadOnlyList<float[]> frames;

        public FakePcmFrameReader(IReadOnlyList<float[]> frames)
        {
            this.frames = frames ?? throw new ArgumentNullException(nameof(frames));
        }

        public Task ReadFramesAsync(
            FfmpegToolPaths toolPaths,
            string inputFilePath,
            int channels,
            IAudioPcmFrameSink frameSink,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(frameSink);

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

    private sealed class CollectingWriter : ISfftAnalysisPointWriter
    {
        public List<SfftAnalysisPoint> Points { get; } = new();

        public void Write(SfftAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            Points.Add(point);
        }
    }
}
