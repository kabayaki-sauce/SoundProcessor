using SoundAnalyzer.Cli.Infrastructure.Postgres;
using SoundAnalyzer.Cli.Infrastructure.Sqlite;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class AnalysisStoreFactory : IAnalysisStoreFactory
{
    public IPeakAnalysisStore CreatePeakStore(CommandLineArguments arguments, SqliteConflictMode conflictMode)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.StorageBackend switch
        {
            StorageBackend.Postgres => new PostgresPeakAnalysisStore(
                BuildPostgresConnectionOptions(arguments),
                BuildPostgresSshOptions(arguments),
                arguments.TableName,
                conflictMode,
                arguments.PostgresBatchRowCount),
            _ => new SqlitePeakAnalysisStore(
                arguments.DbFilePath ?? throw new CliException(CliErrorCode.DbFileRequired, "SQLite mode requires db file path."),
                arguments.TableName,
                conflictMode,
                new SqliteWriteOptions(arguments.SqliteFastMode, arguments.SqliteBatchRowCount)),
        };
    }

    public IStftAnalysisStore CreateStftStore(
        CommandLineArguments arguments,
        string anchorColumnName,
        int binCount,
        SqliteConflictMode conflictMode)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorColumnName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        return arguments.StorageBackend switch
        {
            StorageBackend.Postgres => new PostgresStftAnalysisStore(
                BuildPostgresConnectionOptions(arguments),
                BuildPostgresSshOptions(arguments),
                arguments.TableName,
                anchorColumnName,
                conflictMode,
                binCount,
                arguments.DeleteCurrent,
                arguments.PostgresBatchRowCount),
            _ => new SqliteStftAnalysisStore(
                arguments.DbFilePath ?? throw new CliException(CliErrorCode.DbFileRequired, "SQLite mode requires db file path."),
                arguments.TableName,
                anchorColumnName,
                conflictMode,
                binCount,
                arguments.DeleteCurrent,
                new SqliteWriteOptions(arguments.SqliteFastMode, arguments.SqliteBatchRowCount)),
        };
    }

    private static PostgresConnectionOptions BuildPostgresConnectionOptions(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return new PostgresConnectionOptions(
            arguments.PostgresHost ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-host is required."),
            arguments.PostgresPort ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-port is required."),
            arguments.PostgresDatabase ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-db is required."),
            arguments.PostgresUser ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-user is required."),
            arguments.PostgresPassword,
            arguments.PostgresSslCertPath,
            arguments.PostgresSslKeyPath,
            arguments.PostgresSslRootCertPath);
    }

    private static PostgresSshOptions? BuildPostgresSshOptions(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (string.IsNullOrWhiteSpace(arguments.PostgresSshHost))
        {
            return null;
        }

        return new PostgresSshOptions(
            arguments.PostgresSshHost,
            arguments.PostgresSshPort ?? 22,
            arguments.PostgresSshUser ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-ssh-user is required."),
            arguments.PostgresSshPrivateKeyPath ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-ssh-private-key-path is required."),
            arguments.PostgresSshKnownHostsPath ?? throw new CliException(CliErrorCode.PostgresConfigurationInvalid, "postgres-ssh-known-hosts-path is required."));
    }
}
