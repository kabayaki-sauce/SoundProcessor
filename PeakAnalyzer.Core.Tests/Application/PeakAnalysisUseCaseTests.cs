using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using PeakAnalyzer.Core.Application.Models;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Application.UseCases;
using PeakAnalyzer.Core.Domain.Models;

namespace PeakAnalyzer.Core.Tests.Application;

public sealed class PeakAnalysisUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStartAnchorsAtHopMs()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateMonoFrames(100, 0.5F);
            PeakAnalysisUseCase useCase = BuildUseCase(1000, 1, frames);
            CollectingWriter writer = new();

            PeakAnalysisRequest request = new(
                inputPath,
                "song",
                "Piano",
                windowSizeMs: 50,
                hopMs: 10,
                minLimitDb: -120,
                ffmpegPath: null);

            PeakAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
            float[][] frames =
            [
                [1.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
                [0.0F],
            ];
            PeakAnalysisUseCase useCase = BuildUseCase(1000, 1, frames);
            CollectingWriter writer = new();

            PeakAnalysisRequest request = new(
                inputPath,
                "song",
                "Piano",
                windowSizeMs: 50,
                hopMs: 10,
                minLimitDb: -120,
                ffmpegPath: null);

            PeakAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(1, summary.PointCount);
            Assert.Single(writer.Points);
            Assert.Equal(10, writer.Points[0].Ms);
            Assert.Equal(0, writer.Points[0].Db, precision: 6);
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
            float[][] frames = CreateMonoFrames(10_233, 0.0F);
            PeakAnalysisUseCase useCase = BuildUseCase(1000, 1, frames);
            CollectingWriter writer = new();

            PeakAnalysisRequest request = new(
                inputPath,
                "song",
                "Piano",
                windowSizeMs: 50,
                hopMs: 10,
                minLimitDb: -120,
                ffmpegPath: null);

            PeakAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

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
    public async Task ExecuteAsync_ShouldTakeCrossChannelPeak()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateStereoFrames(20, 0.0F, 0.0F);
            frames[15][0] = 0.2F;
            frames[15][1] = 0.8F;

            PeakAnalysisUseCase useCase = BuildUseCase(1000, 2, frames);
            CollectingWriter writer = new();

            PeakAnalysisRequest request = new(
                inputPath,
                "song",
                "Piano",
                windowSizeMs: 20,
                hopMs: 20,
                minLimitDb: -120,
                ffmpegPath: null);

            _ = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Single(writer.Points);
            double expected = 20 * Math.Log10(0.8);
            Assert.Equal(expected, writer.Points[0].Db, precision: 6);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClampDbByMinLimit()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            float[][] frames = CreateMonoFrames(20, 0.0F);
            PeakAnalysisUseCase useCase = BuildUseCase(1000, 1, frames);
            CollectingWriter writer = new();

            PeakAnalysisRequest request = new(
                inputPath,
                "song",
                "Piano",
                windowSizeMs: 20,
                hopMs: 10,
                minLimitDb: -40,
                ffmpegPath: null);

            PeakAnalysisSummary summary = await useCase.ExecuteAsync(request, writer, CancellationToken.None);

            Assert.Equal(2, summary.PointCount);
            Assert.All(writer.Points, point => Assert.Equal(-40, point.Db, precision: 6));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private static float[][] CreateMonoFrames(int count, float value)
    {
        float[][] frames = new float[count][];
        for (int i = 0; i < count; i++)
        {
            frames[i] = [value];
        }

        return frames;
    }

    private static float[][] CreateStereoFrames(int count, float left, float right)
    {
        float[][] frames = new float[count][];
        for (int i = 0; i < count; i++)
        {
            frames[i] = [left, right];
        }

        return frames;
    }

    private static PeakAnalysisUseCase BuildUseCase(int sampleRate, int channels, float[][] frames)
    {
        AudioStreamInfo streamInfo = new(sampleRate, channels, AudioPcmBitDepth.F32, frames.Length);
        return new PeakAnalysisUseCase(
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
            CancellationToken cancellationToken,
            int? targetSampleRateHz = null)
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

    private sealed class CollectingWriter : IPeakAnalysisPointWriter
    {
        public List<PeakAnalysisPoint> Points { get; } = new();

        public void Write(PeakAnalysisPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            Points.Add(point);
        }
    }
}

