using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using AudioSplitter.Core.Application.Errors;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Core.Domain.Models;
using AudioSplitter.Core.Domain.Services;

namespace AudioSplitter.Core.Application.UseCases;

public sealed class SplitAudioUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly ISilenceAnalyzer silenceAnalyzer;
    private readonly IAudioSegmentExporter segmentExporter;
    private readonly IOverwriteConfirmationService overwriteConfirmationService;

    public SplitAudioUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        ISilenceAnalyzer silenceAnalyzer,
        IAudioSegmentExporter segmentExporter,
        IOverwriteConfirmationService overwriteConfirmationService)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.silenceAnalyzer = silenceAnalyzer ?? throw new ArgumentNullException(nameof(silenceAnalyzer));
        this.segmentExporter = segmentExporter ?? throw new ArgumentNullException(nameof(segmentExporter));
        this.overwriteConfirmationService = overwriteConfirmationService ?? throw new ArgumentNullException(nameof(overwriteConfirmationService));
    }

    public async Task<SplitAudioExecutionResult> ExecuteAsync(
        SplitAudioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.InputFilePath))
        {
            throw new SplitAudioException(SplitAudioErrorCode.InputFileNotFound, request.InputFilePath);
        }

        try
        {
            Directory.CreateDirectory(request.OutputDirectoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SplitAudioException(
                SplitAudioErrorCode.OutputDirectoryCreationFailed,
                request.OutputDirectoryPath,
                ex);
        }

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
            .ConfigureAwait(false);

        OutputAudioFormat outputAudioFormat = request.ResolutionType?.ToOutputAudioFormat()
            ?? OutputAudioFormat.FromInputStream(streamInfo);

        SilenceAnalysisResult analysisResult = await silenceAnalyzer
            .AnalyzeAsync(
                toolPaths,
                request.InputFilePath,
                streamInfo,
                request.LevelDb,
                request.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AudioSegment> segments = SegmentPlanner.Build(
            analysisResult,
            streamInfo.SampleRate,
            request.AfterOffset,
            request.ResumeOffset);

        int generatedCount = 0;
        int skippedCount = 0;
        int promptedCount = 0;

        string baseFileName = Path.GetFileNameWithoutExtension(request.InputFilePath);
        for (int i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AudioSegment segment = segments[i];
            string outputPath = BuildOutputPath(request.OutputDirectoryPath, baseFileName, i + 1);
            OverwriteDecision overwriteDecision = overwriteConfirmationService.Resolve(
                outputPath,
                request.OverwriteWithoutPrompt);
            if (overwriteDecision.Prompted)
            {
                promptedCount++;
            }

            if (!overwriteDecision.ShouldOverwrite)
            {
                skippedCount++;
                continue;
            }

            SegmentExportRequest exportRequest = new(
                request.InputFilePath,
                outputPath,
                segment,
                outputAudioFormat,
                streamInfo.SampleRate);

            await segmentExporter
                .ExportAsync(toolPaths, exportRequest, cancellationToken)
                .ConfigureAwait(false);
            generatedCount++;
        }

        SplitExecutionSummary summary = new(
            generatedCount,
            skippedCount,
            promptedCount,
            segments.Count);
        return new SplitAudioExecutionResult(summary);
    }

    private static string BuildOutputPath(string outputDirectoryPath, string baseFileName, int index)
    {
        ArgumentNullException.ThrowIfNull(outputDirectoryPath);
        ArgumentNullException.ThrowIfNull(baseFileName);

        string formattedIndex = index.ToString("000", CultureInfo.InvariantCulture);
        string fileName = string.Concat(baseFileName, "_", formattedIndex, ".wav");
        return Path.Combine(outputDirectoryPath, fileName);
    }
}
