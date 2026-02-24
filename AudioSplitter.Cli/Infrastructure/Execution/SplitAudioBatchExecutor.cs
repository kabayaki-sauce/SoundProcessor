using CliShared.Application.Models;
using CliShared.Application.Ports;
using AudioSplitter.Cli.Infrastructure.FileSystem;
using AudioSplitter.Cli.Presentation.Cli.Arguments;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.UseCases;

namespace AudioSplitter.Cli.Infrastructure.Execution;

internal sealed class SplitAudioBatchExecutor
{
    private readonly SplitAudioUseCase splitAudioUseCase;

    public SplitAudioBatchExecutor(SplitAudioUseCase splitAudioUseCase)
    {
        this.splitAudioUseCase = splitAudioUseCase ?? throw new ArgumentNullException(nameof(splitAudioUseCase));
    }

    public async Task<SplitAudioBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(arguments, SilentProgressDisplay.Instance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SplitAudioBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        IProgressDisplay progressDisplay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(progressDisplay);

        IReadOnlyList<ResolvedInputAudioFile> targets = ResolveTargets(arguments);
        int processedFileCount = 0;
        int generatedCount = 0;
        int skippedCount = 0;
        int promptedCount = 0;
        int detectedSegmentCount = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResolvedInputAudioFile target = targets[i];
            string outputDirectoryPath = ResolveOutputDirectoryPath(
                arguments.OutputDirectoryPath,
                target.RelativeDirectoryPath);

            SplitAudioRequest request = new(
                target.InputFilePath,
                outputDirectoryPath,
                arguments.LevelDb,
                arguments.Duration,
                arguments.AfterOffset,
                arguments.ResumeOffset,
                arguments.ResolutionType,
                arguments.FfmpegPath,
                arguments.OverwriteWithoutPrompt);

            SplitAudioExecutionResult result = await splitAudioUseCase
                .ExecuteAsync(
                    request,
                    cancellationToken,
                    progress => ReportProgress(progressDisplay, progress, i + 1, targets.Count, target.InputFilePath))
                .ConfigureAwait(false);

            processedFileCount++;
            generatedCount = checked(generatedCount + result.Summary.GeneratedCount);
            skippedCount = checked(skippedCount + result.Summary.SkippedCount);
            promptedCount = checked(promptedCount + result.Summary.PromptedCount);
            detectedSegmentCount = checked(detectedSegmentCount + result.Summary.DetectedSegmentCount);
        }

        return new SplitAudioBatchSummary(
            processedFileCount,
            generatedCount,
            skippedCount,
            promptedCount,
            detectedSegmentCount);
    }

    private static void ReportProgress(
        IProgressDisplay progressDisplay,
        SplitAudioProgress progress,
        int fileIndex,
        int fileCount,
        string inputFilePath)
    {
        ArgumentNullException.ThrowIfNull(progressDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);

        double phaseRatio = ToRatio(progress.Processed, progress.Total);
        double topRatio = (GetPhaseOrder(progress.Phase) + phaseRatio) / 4.0;

        string phaseName = progress.Phase.ToString();
        string fileName = Path.GetFileName(inputFilePath);
        string topLabel = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"File {fileIndex}/{fileCount} {fileName} [{phaseName}]");
        string bottomLabel = progress.Total.HasValue
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{phaseName} {Math.Min(progress.Processed, progress.Total.Value)}/{progress.Total.Value}")
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{phaseName} {progress.Processed}");

        progressDisplay.Report(new DualProgressState(topLabel, topRatio, bottomLabel, phaseRatio));
    }

    private static int GetPhaseOrder(SplitAudioPhase phase)
    {
        return phase switch
        {
            SplitAudioPhase.Resolve => 0,
            SplitAudioPhase.Probe => 1,
            SplitAudioPhase.Analyze => 2,
            SplitAudioPhase.Export => 3,
            _ => 0,
        };
    }

    private static double ToRatio(long processed, long? total)
    {
        if (!total.HasValue || total.Value <= 0)
        {
            return 0;
        }

        double ratio = (double)processed / total.Value;
        if (ratio <= 0)
        {
            return 0;
        }

        if (ratio >= 1)
        {
            return 1;
        }

        return ratio;
    }

    private static IReadOnlyList<ResolvedInputAudioFile> ResolveTargets(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!string.IsNullOrWhiteSpace(arguments.InputFilePath))
        {
            return new[] { new ResolvedInputAudioFile(arguments.InputFilePath, string.Empty) };
        }

        string inputDirectoryPath = arguments.InputDirectoryPath
            ?? throw new ArgumentException("InputDirectoryPath must be specified in directory mode.", nameof(arguments));
        if (!Directory.Exists(inputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, inputDirectoryPath);
        }

        return InputAudioFileResolver.Resolve(inputDirectoryPath, arguments.Recursive);
    }

    private static string ResolveOutputDirectoryPath(string outputRootDirectoryPath, string relativeDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootDirectoryPath);
        ArgumentNullException.ThrowIfNull(relativeDirectoryPath);

        if (string.IsNullOrWhiteSpace(relativeDirectoryPath))
        {
            return outputRootDirectoryPath;
        }

        return Path.Combine(outputRootDirectoryPath, relativeDirectoryPath);
    }

    private sealed class SilentProgressDisplay : IProgressDisplay
    {
        public static SilentProgressDisplay Instance { get; } = new();

        private SilentProgressDisplay()
        {
        }

        public void Report(DualProgressState state)
        {
            _ = state;
        }

        public void Complete()
        {
        }
    }
}
