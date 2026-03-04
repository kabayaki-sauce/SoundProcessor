using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Application.UseCases;
using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Tests.Application;

public sealed class MelSpectrogramInferenceFeatureUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProduceExpectedShape_ForLongWindowLikeInferenceConfig()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter writer = new();

        float[][] waveform =
        [
            CreateSineWave(44100 * 12, 44100, 220.0),
            CreateSineWave(44100 * 12, 44100, 330.0),
        ];

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 44100,
            segmentDurationSeconds: 10.0,
            nowMsList: [10000],
            nFft: 1764,
            winLength: 1764,
            hopLength: 882,
            nMels: 80,
            fMinHz: 20.0,
            fMaxHz: 22050.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        MelSpectrogramInferenceFeatureSummary summary = await useCase.ExecuteAsync(
            request,
            writer,
            framePointWriter: null,
            CancellationToken.None);

        MelSpectrogramInferenceNowTensorPoint point = Assert.Single(writer.Points);
        Assert.Equal(1, summary.NowPointCount);
        Assert.Equal(0, summary.FramePointCount);
        Assert.Equal(2, point.Channels);
        Assert.Equal(80, point.MelBins);
        Assert.Equal(501, point.FrameCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMatchPythonFixture_ForMelLinear()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter writer = new();
        const int sampleRate = 100;

        float[] ch0 = new float[16];
        float[] ch1 = new float[16];
        for (int i = 0; i < 16; i++)
        {
            ch0[i] = i / 20.0F;
            ch1[i] = ch0[15 - i];
        }

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "fixture",
            waveform: [ch0, ch1],
            sampleRate: sampleRate,
            segmentDurationSeconds: 0.16,
            nowMsList: [160],
            nFft: 8,
            winLength: 8,
            hopLength: 2,
            nMels: 4,
            fMinHz: 0.0,
            fMaxHz: 50.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(request, writer, framePointWriter: null, CancellationToken.None);
        MelSpectrogramInferenceNowTensorPoint point = Assert.Single(writer.Points);
        Assert.Equal(4, point.MelBins);
        Assert.Equal(9, point.FrameCount);

        double[][] expected =
        [
            [0.0006165365921333432, 0.04940973222255707, 0.1413242071866989, 0.28506165742874146, 0.48629406094551086, 0.7450213432312012, 1.0612438917160034, 1.4610836505889893, 1.4610841274261475],
            [0.00482734153047204, 0.020781811326742172, 0.05610477551817894, 0.11236731708049774, 0.19113488495349884, 0.29240742325782776, 0.4161851108074188, 0.5753453373908997, 0.575345516204834],
            [0.005602039862424135, 0.0018039249116554856, 0.000945026520639658, 0.0009450258803553879, 0.0009450258803553879, 0.0009450273355469108, 0.0009450261131860316, 0.004248545039445162, 0.004248548299074173],
            [0.0006698131328448653, 0.0003636377223301679, 5.7461638789391145E-05, 5.7461838878225535E-05, 5.7461838878225535E-05, 5.74624355067499E-05, 5.7462639233563095E-05, 0.0006698077777400613, 0.0006698204088024795],
        ];

        for (int mel = 0; mel < expected.Length; mel++)
        {
            for (int frame = 0; frame < expected[mel].Length; frame++)
            {
                Assert.Equal(expected[mel][frame], point.Linear![0, mel, frame], precision: 5);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitDbConsistentWithLinear()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter writer = new();

        float[][] waveform =
        [
            CreateSineWave(2000, 1000, 100),
            CreateSineWave(2000, 1000, 180),
        ];

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 2.0,
            nowMsList: [2000],
            nFft: 100,
            winLength: 100,
            hopLength: 20,
            nMels: 16,
            fMinHz: 0.0,
            fMaxHz: 500.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(request, writer, framePointWriter: null, CancellationToken.None);
        MelSpectrogramInferenceNowTensorPoint point = Assert.Single(writer.Points);

        for (int ch = 0; ch < point.Channels; ch++)
        {
            for (int mel = 0; mel < point.MelBins; mel++)
            {
                for (int frame = 0; frame < point.FrameCount; frame++)
                {
                    double linear = point.Linear![ch, mel, frame];
                    double db = point.Db![ch, mel, frame];
                    if (linear <= 0)
                    {
                        Assert.True(double.IsNegativeInfinity(db));
                        continue;
                    }

                    double expectedDb = 10.0 * Math.Log10(linear);
                    Assert.Equal(expectedDb, db, precision: 6);
                }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClampLinearAndDb_WhenSanitizeIsEnabled()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter writer = new();

        float[][] waveform =
        [
            new float[1000],
            new float[1000],
        ];

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 1.0,
            nowMsList: [1000],
            nFft: 100,
            winLength: 100,
            hopLength: 10,
            nMels: 32,
            fMinHz: 0.0,
            fMaxHz: 500.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: -120.0);

        _ = await useCase.ExecuteAsync(request, writer, framePointWriter: null, CancellationToken.None);
        MelSpectrogramInferenceNowTensorPoint point = Assert.Single(writer.Points);

        const double floor = 1e-12;
        for (int ch = 0; ch < point.Channels; ch++)
        {
            for (int mel = 0; mel < point.MelBins; mel++)
            {
                for (int frame = 0; frame < point.FrameCount; frame++)
                {
                    Assert.Equal(floor, point.Linear![ch, mel, frame], precision: 12);
                    Assert.Equal(-120.0, point.Db![ch, mel, frame], precision: 9);
                }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateDifferentLeftPaddingNoise_OnEachRun()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter writerA = new();
        CollectingNowWriter writerB = new();

        float[][] waveform =
        [
            new float[100],
            new float[100],
        ];

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 100,
            segmentDurationSeconds: 1.0,
            nowMsList: [0],
            nFft: 16,
            winLength: 16,
            hopLength: 4,
            nMels: 8,
            fMinHz: 0.0,
            fMaxHz: 50.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: true,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: false,
            sanitizeMinDbfs: null);

        _ = await useCase.ExecuteAsync(request, writerA, framePointWriter: null, CancellationToken.None);
        _ = await useCase.ExecuteAsync(request, writerB, framePointWriter: null, CancellationToken.None);

        MelSpectrogramInferenceNowTensorPoint pointA = Assert.Single(writerA.Points);
        MelSpectrogramInferenceNowTensorPoint pointB = Assert.Single(writerB.Points);

        bool allFiniteA = AllFinite(pointA.Linear!);
        bool allFiniteB = AllFinite(pointB.Linear!);
        Assert.True(allFiniteA);
        Assert.True(allFiniteB);

        double diffSum = 0;
        for (int ch = 0; ch < pointA.Channels; ch++)
        {
            for (int mel = 0; mel < pointA.MelBins; mel++)
            {
                for (int frame = 0; frame < pointA.FrameCount; frame++)
                {
                    diffSum += Math.Abs(pointA.Linear![ch, mel, frame] - pointB.Linear![ch, mel, frame]);
                }
            }
        }

        Assert.True(diffSum > 1e-9, $"Expected random noise difference but diffSum={diffSum}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepNowTensorAndFrameWriterConsistent()
    {
        MelSpectrogramInferenceWaveformFeatureUseCase useCase = new();
        CollectingNowWriter nowWriter = new();
        CollectingFrameWriter frameWriter = new();

        float[][] waveform =
        [
            CreateSineWave(2000, 1000, 100),
            CreateSineWave(2000, 1000, 180),
        ];

        MelSpectrogramInferenceWaveformRequest request = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 2.0,
            nowMsList: [2000],
            nFft: 100,
            winLength: 100,
            hopLength: 20,
            nMels: 16,
            fMinHz: 0.0,
            fMaxHz: 500.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        MelSpectrogramInferenceFeatureSummary summary = await useCase.ExecuteAsync(
            request,
            nowWriter,
            frameWriter,
            CancellationToken.None);

        MelSpectrogramInferenceNowTensorPoint nowPoint = Assert.Single(nowWriter.Points);
        Assert.Equal(nowPoint.FrameCount, frameWriter.Points.Count);
        Assert.Equal(1, summary.NowPointCount);
        Assert.Equal(nowPoint.FrameCount, summary.FramePointCount);

        for (int frame = 0; frame < nowPoint.FrameCount; frame++)
        {
            MelSpectrogramInferenceFramePoint framePoint = frameWriter.Points[frame];
            for (int ch = 0; ch < nowPoint.Channels; ch++)
            {
                for (int mel = 0; mel < nowPoint.MelBins; mel++)
                {
                    Assert.Equal(nowPoint.Linear![ch, mel, frame], framePoint.Linear![ch, mel], precision: 6);
                    Assert.Equal(nowPoint.Db![ch, mel, frame], framePoint.Db![ch, mel], precision: 6);
                }
            }
        }
    }

    [Fact]
    public async Task FileAndWaveformUseCase_ShouldProduceEquivalentOutput()
    {
        float[][] waveform =
        [
            CreateSineWave(400, 1000, 100),
            CreateSineWave(400, 1000, 170),
        ];
        List<float[]> frames = BuildFrames(waveform);
        CapturingPcmFrameReader pcmReader = new(frames);
        AudioStreamInfo streamInfo = new(1000, 2, AudioPcmBitDepth.F32, frames.Count);
        FakeProbeService probeService = new(streamInfo);

        MelSpectrogramInferenceWaveformFeatureUseCase waveformUseCase = new();
        MelSpectrogramInferenceFileFeatureUseCase fileUseCase = new(
            new FakeFfmpegLocator(),
            probeService,
            pcmReader,
            waveformUseCase);

        MelSpectrogramInferenceWaveformRequest waveformRequest = new(
            name: "song",
            waveform: waveform,
            sampleRate: 1000,
            segmentDurationSeconds: 0.4,
            nowMsList: [400],
            nFft: 64,
            winLength: 64,
            hopLength: 16,
            nMels: 16,
            fMinHz: 0.0,
            fMaxHz: 500.0,
            melPower: 2.0,
            melScale: MelSpectrogramScaleKind.Htk,
            melNorm: MelSpectrogramInferenceNormKind.None,
            center: true,
            padMode: MelSpectrogramInferencePadMode.Reflect,
            leftPadNoiseEnabled: false,
            leftPadNoiseDb: -64.0,
            emitLinear: true,
            emitDb: true,
            sanitizeMinDbfs: null);

        string tempFile = CreateTempInputFile();
        try
        {
            MelSpectrogramInferenceFileRequest fileRequest = new(
                inputFilePath: tempFile,
                name: "song",
                sampleRate: 1000,
                segmentDurationSeconds: 0.4,
                nowMsList: [400],
                nFft: 64,
                winLength: 64,
                hopLength: 16,
                nMels: 16,
                fMinHz: 0.0,
                fMaxHz: 500.0,
                melPower: 2.0,
                melScale: MelSpectrogramScaleKind.Htk,
                melNorm: MelSpectrogramInferenceNormKind.None,
                center: true,
                padMode: MelSpectrogramInferencePadMode.Reflect,
                leftPadNoiseEnabled: false,
                leftPadNoiseDb: -64.0,
                emitLinear: true,
                emitDb: true,
                sanitizeMinDbfs: null,
                ffmpegPath: null);

            CollectingNowWriter waveformWriter = new();
            CollectingNowWriter fileWriter = new();

            _ = await waveformUseCase.ExecuteAsync(waveformRequest, waveformWriter, framePointWriter: null, CancellationToken.None);
            _ = await fileUseCase.ExecuteAsync(fileRequest, fileWriter, framePointWriter: null, CancellationToken.None);

            MelSpectrogramInferenceNowTensorPoint waveformPoint = Assert.Single(waveformWriter.Points);
            MelSpectrogramInferenceNowTensorPoint filePoint = Assert.Single(fileWriter.Points);
            Assert.Equal(waveformPoint.MelBins, filePoint.MelBins);
            Assert.Equal(waveformPoint.FrameCount, filePoint.FrameCount);

            for (int ch = 0; ch < waveformPoint.Channels; ch++)
            {
                for (int mel = 0; mel < waveformPoint.MelBins; mel++)
                {
                    for (int frame = 0; frame < waveformPoint.FrameCount; frame++)
                    {
                        Assert.Equal(waveformPoint.Linear![ch, mel, frame], filePoint.Linear![ch, mel, frame], precision: 6);
                        Assert.Equal(waveformPoint.Db![ch, mel, frame], filePoint.Db![ch, mel, frame], precision: 6);
                    }
                }
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

#pragma warning disable CA1814
    private static bool AllFinite(double[,,] tensor)
    {
        for (int a = 0; a < tensor.GetLength(0); a++)
        {
            for (int b = 0; b < tensor.GetLength(1); b++)
            {
                for (int c = 0; c < tensor.GetLength(2); c++)
                {
                    if (!double.IsFinite(tensor[a, b, c]))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
#pragma warning restore CA1814

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

    private sealed class CollectingNowWriter : IMelSpectrogramInferenceNowTensorPointWriter
    {
        public List<MelSpectrogramInferenceNowTensorPoint> Points { get; } = [];

        public void Write(MelSpectrogramInferenceNowTensorPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            Points.Add(point);
        }
    }

    private sealed class CollectingFrameWriter : IMelSpectrogramInferenceFramePointWriter
    {
        public List<MelSpectrogramInferenceFramePoint> Points { get; } = [];

        public void Write(MelSpectrogramInferenceFramePoint point)
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
