using SFFTAnalyzer.Core.Application.Models;
using SFFTAnalyzer.Core.Application.UseCases;
using SFFTAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class SfftAnalysisBatchExecutor
{
    private readonly SfftAnalysisUseCase sfftAnalysisUseCase;

    public SfftAnalysisBatchExecutor(SfftAnalysisUseCase sfftAnalysisUseCase)
    {
        this.sfftAnalysisUseCase = sfftAnalysisUseCase ?? throw new ArgumentNullException(nameof(sfftAnalysisUseCase));
    }

    public async Task<SfftAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        int binCount = arguments.BinCount ?? throw new CliException(CliErrorCode.UnsupportedMode, arguments.Mode);

        BatchExecutionSupport.EnsureDbDirectory(arguments.DbFilePath);

        ResolvedSfftAudioFiles resolved = SfftAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Recursive);

        SqliteConflictMode conflictMode = BatchExecutionSupport.ResolveConflictMode(arguments);

        long writtenPointCount = 0;
        using SqliteSfftAnalysisStore store = new(
            arguments.DbFilePath,
            arguments.TableName,
            conflictMode,
            binCount,
            arguments.DeleteCurrent);
        store.Initialize();

        foreach (SfftAudioFile target in resolved.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SfftAnalysisRequest request = new(
                target.FilePath,
                target.Name,
                arguments.WindowSizeMs,
                arguments.HopMs,
                binCount,
                arguments.MinLimitDb,
                arguments.FfmpegPath);

            SfftAnalysisSummary summary = await sfftAnalysisUseCase
                .ExecuteAsync(request, store, cancellationToken)
                .ConfigureAwait(false);

            writtenPointCount = checked(writtenPointCount + summary.PointCount);
        }

        store.Complete();

        return new SfftAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            writtenPointCount,
            arguments.TableName,
            binCount);
    }
}
