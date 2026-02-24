using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal static class BatchExecutionSupport
{
    public static SqliteConflictMode ResolveConflictMode(CommandLineArguments arguments)
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

    public static void EnsureDbDirectory(string dbFilePath)
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
