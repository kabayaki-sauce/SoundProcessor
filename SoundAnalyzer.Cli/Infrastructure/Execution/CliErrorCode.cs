namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal enum CliErrorCode
{
    None = 0,
    InputDirectoryNotFound,
    DbDirectoryCreationFailed,
    DbFileRequired,
    DuplicateStftAnalysisName,
    StftTableBinCountMismatch,
    StftTableSchemaMismatch,
    PostgresConfigurationInvalid,
    PostgresCredentialFileNotFound,
    PostgresSshTunnelFailed,
    UnsupportedMode,
}
