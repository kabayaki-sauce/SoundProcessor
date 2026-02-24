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
        ArgumentNullException.ThrowIfNull(arguments);

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
                .ExecuteAsync(request, cancellationToken)
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
}
