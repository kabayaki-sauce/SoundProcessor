using PeakAnalyzer.Core.Application.Models;
using PeakAnalyzer.Core.Application.UseCases;
using PeakAnalyzer.Core.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class PeakAnalysisBatchExecutor
{
    private readonly PeakAnalysisUseCase peakAnalysisUseCase;

    public PeakAnalysisBatchExecutor(PeakAnalysisUseCase peakAnalysisUseCase)
    {
        this.peakAnalysisUseCase = peakAnalysisUseCase ?? throw new ArgumentNullException(nameof(peakAnalysisUseCase));
    }

    public async Task<PeakAnalysisBatchSummary> ExecuteAsync(
        CommandLineArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!Directory.Exists(arguments.InputDirectoryPath))
        {
            throw new CliException(CliErrorCode.InputDirectoryNotFound, arguments.InputDirectoryPath);
        }

        EnsureDbDirectory(arguments.DbFilePath);

        ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(
            arguments.InputDirectoryPath,
            arguments.Stems);

        SqliteConflictMode conflictMode = ResolveConflictMode(arguments);

        long writtenPointCount = 0;
        using SqlitePeakAnalysisStore store = new(arguments.DbFilePath, arguments.TableName, conflictMode);
        store.Initialize();

        foreach (StemAudioFile target in resolved.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PeakAnalysisRequest request = new(
                target.FilePath,
                target.Name,
                target.Stem,
                arguments.WindowSizeMs,
                arguments.HopMs,
                arguments.MinLimitDb,
                arguments.FfmpegPath);

            PeakAnalysisSummary summary = await peakAnalysisUseCase
                .ExecuteAsync(request, store, cancellationToken)
                .ConfigureAwait(false);

            writtenPointCount = checked(writtenPointCount + summary.PointCount);
        }

        store.Complete();

        return new PeakAnalysisBatchSummary(
            resolved.DirectoryCount,
            resolved.Files.Count,
            resolved.SkippedStemCount,
            writtenPointCount,
            arguments.TableName);
    }

    private static SqliteConflictMode ResolveConflictMode(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Upsert)
        {
            return SqliteConflictMode.Upsert;
        }

        if (arguments.SkipDuplicate)
        {
            return SqliteConflictMode.SkipDuplicate;
        }

        return SqliteConflictMode.Error;
    }

    private static void EnsureDbDirectory(string dbFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);

        string? directoryPath = Path.GetDirectoryName(dbFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CliException(CliErrorCode.DbDirectoryCreationFailed, directoryPath, ex);
        }
    }
}
