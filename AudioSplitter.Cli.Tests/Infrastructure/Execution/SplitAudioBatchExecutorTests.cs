using AudioProcessor.Application.Errors;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using AudioSplitter.Cli.Infrastructure.Execution;
using AudioSplitter.Cli.Presentation.Cli.Arguments;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Core.Application.UseCases;
using AudioSplitter.Core.Domain.Models;
using AudioSplitter.Core.Domain.ValueObjects;

namespace AudioSplitter.Cli.Tests.Infrastructure.Execution;

public sealed class SplitAudioBatchExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldAggregateSummary_WhenInputFileIsSpecified()
    {
        string inputPath = await CreateTempInputFileAsync("single-input.wav");
        string outputDirectoryPath = CreateTempDirectory();
        try
        {
            FakeSegmentExporter exporter = new();
            SplitAudioBatchExecutor executor = BuildExecutor(exporter);
            CommandLineArguments arguments = CreateInputFileArguments(inputPath, outputDirectoryPath);

            SplitAudioBatchSummary summary = await executor.ExecuteAsync(arguments, CancellationToken.None);

            Assert.Equal(1, summary.ProcessedFileCount);
            Assert.Equal(1, summary.GeneratedCount);
            Assert.Equal(0, summary.SkippedCount);
            Assert.Equal(0, summary.PromptedCount);
            Assert.Equal(1, summary.DetectedSegmentCount);
            Assert.Single(exporter.Requests);
            string expected = Path.Combine(outputDirectoryPath, "single-input_001.wav");
            Assert.Equal(expected, exporter.Requests[0].OutputFilePath, ignoreCase: true);
        }
        finally
        {
            File.Delete(inputPath);
            string? inputDirectoryPath = Path.GetDirectoryName(inputPath);
            if (!string.IsNullOrWhiteSpace(inputDirectoryPath) && Directory.Exists(inputDirectoryPath))
            {
                Directory.Delete(inputDirectoryPath, recursive: true);
            }

            Directory.Delete(outputDirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveRelativeDirectory_WhenInputDirIsSpecified()
    {
        string inputRootPath = CreateTempDirectory();
        string outputRootPath = CreateTempDirectory();
        try
        {
            string subDirectoryPath = Path.Combine(inputRootPath, "Sub");
            Directory.CreateDirectory(subDirectoryPath);
            string inputPath = Path.Combine(subDirectoryPath, "file.wav");
            await File.WriteAllTextAsync(inputPath, "content");

            FakeSegmentExporter exporter = new();
            SplitAudioBatchExecutor executor = BuildExecutor(exporter);
            CommandLineArguments arguments = CreateInputDirectoryArguments(inputRootPath, outputRootPath, recursive: true);

            SplitAudioBatchSummary summary = await executor.ExecuteAsync(arguments, CancellationToken.None);

            Assert.Equal(1, summary.ProcessedFileCount);
            Assert.Equal(1, summary.GeneratedCount);
            Assert.Single(exporter.Requests);
            string expectedDirectoryPath = Path.Combine(outputRootPath, "Sub");
            string actualDirectoryPath = Path.GetDirectoryName(exporter.Requests[0].OutputFilePath) ?? string.Empty;
            Assert.Equal(expectedDirectoryPath, actualDirectoryPath, ignoreCase: true);
            Assert.Equal("file_001.wav", Path.GetFileName(exporter.Requests[0].OutputFilePath), StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(inputRootPath, recursive: true);
            Directory.Delete(outputRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZeroSummary_WhenNoSupportedFileExists()
    {
        string inputRootPath = CreateTempDirectory();
        string outputRootPath = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(inputRootPath, "note.txt"), "text");

            FakeSegmentExporter exporter = new();
            SplitAudioBatchExecutor executor = BuildExecutor(exporter);
            CommandLineArguments arguments = CreateInputDirectoryArguments(inputRootPath, outputRootPath, recursive: true);

            SplitAudioBatchSummary summary = await executor.ExecuteAsync(arguments, CancellationToken.None);

            Assert.Equal(0, summary.ProcessedFileCount);
            Assert.Equal(0, summary.GeneratedCount);
            Assert.Equal(0, summary.SkippedCount);
            Assert.Equal(0, summary.PromptedCount);
            Assert.Equal(0, summary.DetectedSegmentCount);
            Assert.Empty(exporter.Requests);
        }
        finally
        {
            Directory.Delete(inputRootPath, recursive: true);
            Directory.Delete(outputRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailFast_WhenExportFails()
    {
        string inputRootPath = CreateTempDirectory();
        string outputRootPath = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(inputRootPath, "a.wav"), "a");
            await File.WriteAllTextAsync(Path.Combine(inputRootPath, "b.wav"), "b");

            FakeSegmentExporter exporter = new(failAtRequestNumber: 1);
            SplitAudioBatchExecutor executor = BuildExecutor(exporter);
            CommandLineArguments arguments = CreateInputDirectoryArguments(inputRootPath, outputRootPath, recursive: false);

            await Assert.ThrowsAsync<AudioProcessorException>(
                () => executor.ExecuteAsync(arguments, CancellationToken.None));
            Assert.Single(exporter.Requests);
        }
        finally
        {
            Directory.Delete(inputRootPath, recursive: true);
            Directory.Delete(outputRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowCliException_WhenInputDirectoryDoesNotExist()
    {
        string outputRootPath = CreateTempDirectory();
        try
        {
            string missingInputDirectoryPath = Path.Combine(outputRootPath, "missing-input");
            FakeSegmentExporter exporter = new();
            SplitAudioBatchExecutor executor = BuildExecutor(exporter);
            CommandLineArguments arguments = CreateInputDirectoryArguments(
                missingInputDirectoryPath,
                outputRootPath,
                recursive: false);

            CliException exception = await Assert.ThrowsAsync<CliException>(
                () => executor.ExecuteAsync(arguments, CancellationToken.None));
            Assert.Equal(CliErrorCode.InputDirectoryNotFound, exception.ErrorCode);
        }
        finally
        {
            Directory.Delete(outputRootPath, recursive: true);
        }
    }

    private static SplitAudioBatchExecutor BuildExecutor(FakeSegmentExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);

        SplitAudioUseCase useCase = new(
            new FakeFfmpegLocator(),
            new FakeProbeService(new AudioStreamInfo(44_100, 2, AudioPcmBitDepth.Pcm24, 1_000)),
            new FakeSilenceAnalyzer(new SilenceAnalysisResult(1_000, 0, Array.Empty<SilenceRun>())),
            exporter,
            new AlwaysOverwriteService());
        return new SplitAudioBatchExecutor(useCase);
    }

    private static CommandLineArguments CreateInputFileArguments(string inputFilePath, string outputDirectoryPath)
    {
        return new CommandLineArguments(
            inputFilePath,
            inputDirectoryPath: null,
            outputDirectoryPath,
            levelDb: -48.0,
            duration: TimeSpan.FromMilliseconds(2_000),
            afterOffset: TimeSpan.Zero,
            resumeOffset: TimeSpan.Zero,
            resolutionType: null,
            ffmpegPath: null,
            overwriteWithoutPrompt: true,
            recursive: false,
            progress: false);
    }

    private static CommandLineArguments CreateInputDirectoryArguments(
        string inputDirectoryPath,
        string outputDirectoryPath,
        bool recursive)
    {
        return new CommandLineArguments(
            inputFilePath: null,
            inputDirectoryPath,
            outputDirectoryPath,
            levelDb: -48.0,
            duration: TimeSpan.FromMilliseconds(2_000),
            afterOffset: TimeSpan.Zero,
            resumeOffset: TimeSpan.Zero,
            resolutionType: null,
            ffmpegPath: null,
            overwriteWithoutPrompt: true,
            recursive,
            progress: false);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"audio-splitter-batch-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<string> CreateTempInputFileAsync(string fileName)
    {
        string directoryPath = CreateTempDirectory();
        string filePath = Path.Combine(directoryPath, fileName);
        await File.WriteAllTextAsync(filePath, "input").ConfigureAwait(false);
        return filePath;
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
            CancellationToken cancellationToken,
            Action<SilenceAnalysisProgress>? progressCallback = null)
        {
            return Task.FromResult(analysisResult);
        }
    }

    private sealed class FakeSegmentExporter : IAudioSegmentExporter
    {
        private readonly int failAtRequestNumber;

        public FakeSegmentExporter(int failAtRequestNumber = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(failAtRequestNumber);
            this.failAtRequestNumber = failAtRequestNumber;
        }

        public List<SegmentExportRequest> Requests { get; } = new();

        public Task ExportAsync(
            FfmpegToolPaths toolPaths,
            SegmentExportRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (failAtRequestNumber > 0 && Requests.Count == failAtRequestNumber)
            {
                throw new AudioProcessorException(AudioProcessorErrorCode.ExportFailed, "Simulated export failure");
            }

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
}
