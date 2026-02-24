using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal interface IAnalysisStoreFactory
{
    public IPeakAnalysisStore CreatePeakStore(CommandLineArguments arguments, SqliteConflictMode conflictMode);

    public IStftAnalysisStore CreateStftStore(
        CommandLineArguments arguments,
        string anchorColumnName,
        int binCount,
        SqliteConflictMode conflictMode);
}
