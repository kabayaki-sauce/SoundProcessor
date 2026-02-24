using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Core.Application.UseCases;
using AudioSplitter.Core.Domain.Models;
using AudioSplitter.Core.Domain.ValueObjects;

namespace AudioSplitter.Core.Tests.Application;

public sealed class SplitAudioUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPreserveInputResolution_WhenResolutionTypeIsNull()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            FakeSegmentExporter exporter = new();
            SplitAudioUseCase useCase = BuildUseCase(
                streamInfo: new AudioStreamInfo(44_100, 2, AudioPcmBitDepth.Pcm24, 1_000),
                analysisResult: new SilenceAnalysisResult(1_000, 0, Array.Empty<SilenceRun>()),
                exporter: exporter,
                overwrite: new AlwaysOverwriteService());

            SplitAudioRequest request = new(
                inputPath,
                Path.GetTempPath(),
                -48.0,
                TimeSpan.FromMilliseconds(2_000),
                TimeSpan.Zero,
                TimeSpan.Zero,
                resolutionType: null,
                ffmpegPath: null,
                overwriteWithoutPrompt: true);

            SplitAudioExecutionResult result = await useCase.ExecuteAsync(request, CancellationToken.None);

            Assert.Equal(1, result.Summary.GeneratedCount);
            Assert.Single(exporter.Requests);
            Assert.Equal(AudioPcmBitDepth.Pcm24, exporter.Requests[0].OutputFormat.BitDepth);
            Assert.Equal(44_100, exporter.Requests[0].OutputFormat.SampleRate);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseResolutionType_WhenSpecified()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            FakeSegmentExporter exporter = new();
            SplitAudioUseCase useCase = BuildUseCase(
                streamInfo: new AudioStreamInfo(96_000, 2, AudioPcmBitDepth.F32, 1_000),
                analysisResult: new SilenceAnalysisResult(1_000, 0, Array.Empty<SilenceRun>()),
                exporter: exporter,
                overwrite: new AlwaysOverwriteService());

            bool parsed = ResolutionType.TryParse("24bit,44100hz", out ResolutionType resolutionType);
            Assert.True(parsed);

            SplitAudioRequest request = new(
                inputPath,
                Path.GetTempPath(),
                -48.0,
                TimeSpan.FromMilliseconds(2_000),
                TimeSpan.Zero,
                TimeSpan.Zero,
                resolutionType,
                ffmpegPath: null,
                overwriteWithoutPrompt: true);

            await useCase.ExecuteAsync(request, CancellationToken.None);

            Assert.Single(exporter.Requests);
            Assert.Equal(AudioPcmBitDepth.Pcm24, exporter.Requests[0].OutputFormat.BitDepth);
            Assert.Equal(44_100, exporter.Requests[0].OutputFormat.SampleRate);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinue_WhenOneOutputIsSkipped()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            FakeSegmentExporter exporter = new();
            SequenceOverwriteService overwrite = new(
                new OverwriteDecision(false, true),
                new OverwriteDecision(true, true));
            SilenceAnalysisResult analysis = new(
                10_000,
                0,
                new[] { new SilenceRun(3_000, 6_000) });

            SplitAudioUseCase useCase = BuildUseCase(
                streamInfo: new AudioStreamInfo(1_000, 2, AudioPcmBitDepth.Pcm16, 10_000),
                analysisResult: analysis,
                exporter: exporter,
                overwrite: overwrite);

            SplitAudioRequest request = new(
                inputPath,
                Path.GetTempPath(),
                -48.0,
                TimeSpan.FromMilliseconds(2_000),
                TimeSpan.Zero,
                TimeSpan.Zero,
                resolutionType: null,
                ffmpegPath: null,
                overwriteWithoutPrompt: false);

            SplitAudioExecutionResult result = await useCase.ExecuteAsync(request, CancellationToken.None);

            Assert.Equal(1, result.Summary.GeneratedCount);
            Assert.Equal(1, result.Summary.SkippedCount);
            Assert.Equal(2, result.Summary.PromptedCount);
            Assert.Equal(2, result.Summary.DetectedSegmentCount);
            Assert.Single(exporter.Requests);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZeroSegments_WhenAllSilent()
    {
        string inputPath = CreateTempInputFile();
        try
        {
            FakeSegmentExporter exporter = new();
            SplitAudioUseCase useCase = BuildUseCase(
                streamInfo: new AudioStreamInfo(44_100, 2, AudioPcmBitDepth.Pcm24, 1_000),
                analysisResult: new SilenceAnalysisResult(1_000, null, new[] { new SilenceRun(0, 1_000) }),
                exporter: exporter,
                overwrite: new AlwaysOverwriteService());

            SplitAudioRequest request = new(
                inputPath,
                Path.GetTempPath(),
                -48.0,
                TimeSpan.FromMilliseconds(2_000),
                TimeSpan.Zero,
                TimeSpan.Zero,
                resolutionType: null,
                ffmpegPath: null,
                overwriteWithoutPrompt: true);

            SplitAudioExecutionResult result = await useCase.ExecuteAsync(request, CancellationToken.None);

            Assert.Equal(0, result.Summary.GeneratedCount);
            Assert.Equal(0, result.Summary.DetectedSegmentCount);
            Assert.Empty(exporter.Requests);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private static SplitAudioUseCase BuildUseCase(
        AudioStreamInfo streamInfo,
        SilenceAnalysisResult analysisResult,
        FakeSegmentExporter exporter,
        IOverwriteConfirmationService overwrite)
    {
        return new SplitAudioUseCase(
            new FakeFfmpegLocator(),
            new FakeProbeService(streamInfo),
            new FakeSilenceAnalyzer(analysisResult),
            exporter,
            overwrite);
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

    private sealed class FakeSilenceAnalyzer : ISilenceAnalyzer
    {
        private readonly SilenceAnalysisResult analysisResult;

        public FakeSilenceAnalyzer(SilenceAnalysisResult analysisResult)
        {
            this.analysisResult = analysisResult ?? throw new ArgumentNullException(nameof(analysisResult));
        }

        public Task<SilenceAnalysisResult> AnalyzeAsync(
            FfmpegToolPaths toolPaths,
            string inputFilePath,
            AudioStreamInfo streamInfo,
            double levelDb,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(analysisResult);
        }
    }

    private sealed class FakeSegmentExporter : IAudioSegmentExporter
    {
        public List<SegmentExportRequest> Requests { get; } = new();

        public Task ExportAsync(
            FfmpegToolPaths toolPaths,
            SegmentExportRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysOverwriteService : IOverwriteConfirmationService
    {
        public OverwriteDecision Resolve(string outputPath, bool overwriteWithoutPrompt)
        {
            return new OverwriteDecision(true, false);
        }
    }

    private sealed class SequenceOverwriteService : IOverwriteConfirmationService
    {
        private readonly Queue<OverwriteDecision> decisions;

        public SequenceOverwriteService(params OverwriteDecision[] decisions)
        {
            ArgumentNullException.ThrowIfNull(decisions);
            this.decisions = new Queue<OverwriteDecision>(decisions);
        }

        public OverwriteDecision Resolve(string outputPath, bool overwriteWithoutPrompt)
        {
            if (decisions.Count == 0)
            {
                return new OverwriteDecision(true, false);
            }

            return decisions.Dequeue();
        }
    }
}


