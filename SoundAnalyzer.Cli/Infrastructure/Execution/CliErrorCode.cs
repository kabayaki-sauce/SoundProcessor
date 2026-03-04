namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal enum CliErrorCode
{
    None = 0,
    InputDirectoryNotFound,
    DbDirectoryCreationFailed,
    DbFileRequired,
    DuplicateStftAnalysisName,
    DuplicateMelAnalysisName,
    StftTableBinCountMismatch,
    StftTableSchemaMismatch,
    MelTableBinCountMismatch,
    MelTableSchemaMismatch,
    PostgresConfigurationInvalid,
    PostgresCredentialFileNotFound,
    PostgresSshTunnelFailed,
    UnsupportedMode,
}
