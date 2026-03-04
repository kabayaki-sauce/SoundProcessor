using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Application.UseCases;
using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Tests.Application;

public sealed class StftInferenceFeatureUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProduceExpectedShape_ForR03ShortWindowLikeParameters()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();

        float[][] waveform =
        [
            CreateSineWave(44100 * 4, 44100, 220.0),
            CreateSineWave(44100 * 4, 44100, 330.0),
        ];

        StftInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 44100,
            segmentDurationSeconds: 3.0,
            nowMsList: [3000],
            nFft: 3528,
            winLength: 3528,
            hopLength: 441,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        StftInferenceFeatureSummary summary = await useCase.ExecuteAsync(
            request,
            nowWriter,
            framePointWriter: null,
            CancellationToken.None);

        StftInferenceNowTensorPoint point = Assert.Single(nowWriter.Points);
        Assert.Equal(1, summary.NowPointCount);
        Assert.Equal(0, summary.FramePointCount);
        Assert.Equal(2, point.Channels);
        Assert.Equal(1765, point.FrequencyBins);
        Assert.Equal(301, point.FrameCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSupportNonPowerOfTwoNfft_AndMatchPythonFixtureForStftLinear()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();
        const int sampleRate = 100;

        float[] ch0 = new float[16];
        float[] ch1 = new float[16];
        for (int i = 0; i < 16; i++)
        {
            ch0[i] = i / 20.0F;
            ch1[i] = ch0[15 - i];
        }

        StftInferenceWaveformRequest request = new(
            name: "fixture",
            waveform: [ch0, ch1],
            sampleRate: sampleRate,
            segmentDurationSeconds: 0.16,
            nowMsList: [160],
            nFft: 8,
            winLength: 8,
            hopLength: 2,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(request, nowWriter, framePointWriter: null, CancellationToken.None);
        StftInferenceNowTensorPoint point = Assert.Single(nowWriter.Points);
        Assert.NotNull(point.Linear);
        Assert.NotNull(point.Db);
        Assert.Equal(5, point.FrequencyBins);
        Assert.Equal(9, point.FrameCount);

        double[][] expected =
        [
            [0.22928932309150696, 0.41464459896087646, 0.7999999523162842, 1.1999999284744263, 1.5999999046325684, 1.9999998807907104, 2.3999998569488525, 2.720710515975952, 2.720710515975952],
            [0.029289331287145615, 0.2622021734714508, 0.44344350695610046, 0.6297953128814697, 0.8225826025009155, 1.0181561708450317, 1.2151716947555542, 1.4258294105529785, 1.4258296489715576],
            [0.10000000149011612, 0.05606602877378464, 0.04142136871814728, 0.041421353816986084, 0.041421353816986084, 0.04142138361930847, 0.041421353816986084, 0.086602583527565, 0.086602583527565],
            [0.029289331287145615, 0.021580778062343597, 0.008578702807426453, 0.008578717708587646, 0.008578717708587646, 0.008578762412071228, 0.008578777313232422, 0.029289213940501213, 0.0292894896119833],
            [0.029289312660694122, 0.014644607901573181, 8.940696716308594E-08, 1.1920928955078125E-07, 1.1920928955078125E-07, 1.1920928955078125E-07, 2.384185791015625E-07, 0.02071070671081543, 0.02071070671081543],
        ];

        for (int freq = 0; freq < expected.Length; freq++)
        {
            for (int frame = 0; frame < expected[freq].Length; frame++)
            {
                Assert.Equal(expected[freq][frame], point.Linear![0, freq, frame], precision: 5);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitDbConsistentWithLinear()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();

        float[][] waveform =
        [
            CreateSineWave(4000, 1000, 120.0),
            CreateSineWave(4000, 1000, 170.0),
        ];

        StftInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 2.0,
            nowMsList: [2000],
            nFft: 100,
            winLength: 100,
            hopLength: 10,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(request, nowWriter, framePointWriter: null, CancellationToken.None);
        StftInferenceNowTensorPoint point = Assert.Single(nowWriter.Points);

        for (int ch = 0; ch < point.Channels; ch++)
        {
            for (int freq = 0; freq < point.FrequencyBins; freq++)
            {
                for (int frame = 0; frame < point.FrameCount; frame++)
                {
                    double linear = point.Linear![ch, freq, frame];
                    double db = point.Db![ch, freq, frame];
                    if (linear <= 0)
                    {
                        Assert.True(double.IsNegativeInfinity(db));
                        continue;
                    }

                    double expectedDb = 20.0 * Math.Log10(linear);
                    Assert.Equal(expectedDb, db, precision: 6);
                }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClampLinearAndDb_WhenSanitizeIsEnabled()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();

        float[][] waveform =
        [
            new float[1000],
            new float[1000],
        ];

        StftInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 1.0,
            nowMsList: [1000],
            nFft: 100,
            winLength: 100,
            hopLength: 10,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: -120.0);

        _ = await useCase.ExecuteAsync(request, nowWriter, framePointWriter: null, CancellationToken.None);
        StftInferenceNowTensorPoint point = Assert.Single(nowWriter.Points);

        const double floor = 1e-6;
        for (int ch = 0; ch < point.Channels; ch++)
        {
            for (int freq = 0; freq < point.FrequencyBins; freq++)
            {
                for (int frame = 0; frame < point.FrameCount; frame++)
                {
                    Assert.Equal(floor, point.Linear![ch, freq, frame], precision: 9);
                    Assert.Equal(-120.0, point.Db![ch, freq, frame], precision: 9);
                }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProduceDifferentResult_WhenCenterModeChanges()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter centerWriter = new();
        CollectingNowWriter nonCenterWriter = new();

        float[] ramp = new float[64];
        for (int i = 0; i < ramp.Length; i++)
        {
            ramp[i] = i / 64.0F;
        }

        StftInferenceWaveformRequest centerRequest = new(
            name: "song",
            waveform: [ramp, ramp],
            sampleRate: 100,
            segmentDurationSeconds: 0.64,
            nowMsList: [640],
            nFft: 8,
            winLength: 8,
            hopLength: 2,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: false,
            sanitizeMinDbfs: null);

        StftInferenceWaveformRequest nonCenterRequest = new(
            name: "song",
            waveform: [ramp, ramp],
            sampleRate: 100,
            segmentDurationSeconds: 0.64,
            nowMsList: [640],
            nFft: 8,
            winLength: 8,
            hopLength: 2,
            power: 1.0,
            center: false,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: false,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(centerRequest, centerWriter, framePointWriter: null, CancellationToken.None);
        _ = await useCase.ExecuteAsync(nonCenterRequest, nonCenterWriter, framePointWriter: null, CancellationToken.None);

        StftInferenceNowTensorPoint centerPoint = Assert.Single(centerWriter.Points);
        StftInferenceNowTensorPoint nonCenterPoint = Assert.Single(nonCenterWriter.Points);
        Assert.NotEqual(centerPoint.FrameCount, nonCenterPoint.FrameCount);
        Assert.NotEqual(centerPoint.Linear![0, 0, 0], nonCenterPoint.Linear![0, 0, 0], precision: 6);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepNowTensorAndFrameWriterConsistent()
    {
        StftInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();
        CollectingFrameWriter frameWriter = new();

        float[][] waveform =
        [
            CreateSineWave(1000, 1000, 90),
            CreateSineWave(1000, 1000, 140),
        ];

        StftInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 1.0,
            nowMsList: [1000],
            nFft: 64,
            winLength: 64,
            hopLength: 16,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        StftInferenceFeatureSummary summary = await useCase.ExecuteAsync(request, nowWriter, frameWriter, CancellationToken.None);
        StftInferenceNowTensorPoint nowPoint = Assert.Single(nowWriter.Points);

        Assert.Equal(nowPoint.FrameCount, frameWriter.Points.Count);
        Assert.Equal(1, summary.NowPointCount);
        Assert.Equal(nowPoint.FrameCount, summary.FramePointCount);

        for (int frame = 0; frame < nowPoint.FrameCount; frame++)
        {
            StftInferenceFramePoint framePoint = frameWriter.Points[frame];
            Assert.Equal(frame, framePoint.FrameIndex);
            for (int ch = 0; ch < nowPoint.Channels; ch++)
            {
                for (int freq = 0; freq < nowPoint.FrequencyBins; freq++)
                {
                    Assert.Equal(nowPoint.Linear![ch, freq, frame], framePoint.Linear![ch, freq], precision: 6);
                    Assert.Equal(nowPoint.Db![ch, freq, frame], framePoint.Db![ch, freq], precision: 6);
                }
            }
        }
    }

    [Fact]
    public async Task FileAndWaveformUseCase_ShouldProduceEquivalentOutput()
    {
        float[][] waveform =
        [
            CreateSineWave(200, 1000, 120),
            CreateSineWave(200, 1000, 170),
        ];
        List<float[]> frames = BuildFrames(waveform);
        CapturingPcmFrameReader pcmReader = new(frames);
        AudioStreamInfo streamInfo = new(1000, 2, AudioPcmBitDepth.F32, frames.Count);
        FakeProbeService probeService = new(streamInfo);

        StftInferenceWaveformFeatureUseCase waveformUseCase = new();
        StftInferenceFileFeatureUseCase fileUseCase = new(
            new FakeFfmpegLocator(),
            probeService,
            pcmReader,
            waveformUseCase);

        StftInferenceWaveformRequest waveformRequest = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 0.2,
            nowMsList: [200],
            nFft: 32,
            winLength: 32,
            hopLength: 8,
            power: 1.0,
            center: true,
            padMode: StftInferencePadMode.Reflect,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        string tempFile = CreateTempInputFile();
        try
        {
            StftInferenceFileRequest fileRequest = new(
                inputFilePath: tempFile,
                name: "song",
                sampleRate: 1000,
                segmentDurationSeconds: 0.2,
                nowMsList: [200],
                nFft: 32,
                winLength: 32,
                hopLength: 8,
                power: 1.0,
                center: true,
                padMode: StftInferencePadMode.Reflect,
                emitLinear: true,
                emitDb: true,
                sanitizeMinDbfs: null,
                ffmpegPath: null);

            CollectingNowWriter waveformWriter = new();
            CollectingNowWriter fileWriter = new();

            _ = await waveformUseCase.ExecuteAsync(waveformRequest, waveformWriter, framePointWriter: null, CancellationToken.None);
            _ = await fileUseCase.ExecuteAsync(fileRequest, fileWriter, framePointWriter: null, CancellationToken.None);

            StftInferenceNowTensorPoint waveformPoint = Assert.Single(waveformWriter.Points);
            StftInferenceNowTensorPoint filePoint = Assert.Single(fileWriter.Points);

            Assert.Equal(waveformPoint.FrequencyBins, filePoint.FrequencyBins);
            Assert.Equal(waveformPoint.FrameCount, filePoint.FrameCount);
            for (int ch = 0; ch < waveformPoint.Channels; ch++)
            {
                for (int freq = 0; freq < waveformPoint.FrequencyBins; freq++)
                {
                    for (int frame = 0; frame < waveformPoint.FrameCount; frame++)
                    {
                        Assert.Equal(waveformPoint.Linear![ch, freq, frame], filePoint.Linear![ch, freq, frame], precision: 6);
                        Assert.Equal(waveformPoint.Db![ch, freq, frame], filePoint.Db![ch, freq, frame], precision: 6);
                    }
                }
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static float CreateSineSample(int index, int sampleRate, double frequencyHz)
    {
        double value = Math.Sin(2.0 * Math.PI * frequencyHz * index / sampleRate);
        return (float)value;
    }

    private static float[] CreateSineWave(int sampleCount, int sampleRate, double frequencyHz)
    {
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = CreateSineSample(i, sampleRate, frequencyHz);
        }

        return samples;
    }

    private static List<float[]> BuildFrames(float[][] waveform)
    {
        int channels = waveform.Length;
        int sampleCount = waveform[0].Length;
        List<float[]> frames = new(sampleCount);

        for (int sample = 0; sample < sampleCount; sample++)
        {
            float[] frame = new float[channels];
            for (int ch = 0; ch < channels; ch++)
            {
                frame[ch] = waveform[ch][sample];
            }

            frames.Add(frame);
        }

        return frames;
    }

    private static string CreateTempInputFile()
    {
        string path = Path.GetTempFileName();
        using FileStream stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        stream.WriteByte(0);
        return path;
    }

    private sealed class CollectingNowWriter : IStftInferenceNowTensorPointWriter
    {
        public List<StftInferenceNowTensorPoint> Points { get; } = [];

        public void Write(StftInferenceNowTensorPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            Points.Add(point);
        }
    }

    private sealed class CollectingFrameWriter : IStftInferenceFramePointWriter
    {
        public List<StftInferenceFramePoint> Points { get; } = [];

        public void Write(StftInferenceFramePoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            Points.Add(point);
        }
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
}
