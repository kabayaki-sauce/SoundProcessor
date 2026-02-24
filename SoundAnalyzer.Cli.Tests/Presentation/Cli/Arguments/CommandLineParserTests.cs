using SoundAnalyzer.Cli.Presentation.Cli.Arguments;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Tests.Presentation.Cli.Arguments;

public sealed class CommandLineParserTests
{
    private static readonly string[] BasePeakArgs =
    [
        "--window-size", "50ms",
        "--hop", "10ms",
        "--input-dir", "C:/input",
        "--db-file", "C:/tmp/analysis.db",
        "--mode", "peak-analysis",
    ];

    private static readonly string[] BaseStftArgs =
    [
        "--window-size", "50ms",
        "--hop", "10ms",
        "--input-dir", "C:/input",
        "--db-file", "C:/tmp/analysis.db",
        "--mode", "stft-analysis",
        "--bin-count", "12",
    ];

    private static readonly string[] BasePostgresStftArgs =
    [
        "--window-size", "50ms",
        "--hop", "10ms",
        "--input-dir", "/mnt/audio/input",
        "--mode", "stft-analysis",
        "--bin-count", "12",
        "--postgres",
        "--postgres-host", "localhost",
        "--postgres-port", "5432",
        "--postgres-db", "audio",
        "--postgres-user", "analyzer",
    ];

    [Fact]
    public void Parse_ShouldFail_WhenUpsertAndSkipDuplicateAreSpecifiedTogether()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--upsert",
            "--skip-duplicate",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.UpsertSkipConflictText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldAcceptTableNameWithHyphenAndUnderscore()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--table-name-override", "T-PEAK_01",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal("T-PEAK_01", result.Arguments.TableName, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRejectInvalidTableName()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--table-name-override", "T PEAK",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("Invalid table name", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldRequireBinCount_WhenModeIsStft()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "stft-analysis",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => string.Equals(
                error,
                ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.BinCountOption),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldRejectLegacySfftMode()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "sfft-analysis",
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("Unsupported mode", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldWarnAndIgnoreStems_WhenModeIsStft()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--stems", "Piano",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Null(result.Arguments.Stems);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(ConsoleTexts.StemsOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldWarnAndIgnoreBinCount_WhenModeIsPeak()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Null(result.Arguments.BinCount);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(ConsoleTexts.BinCountOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldWarnAndIgnoreRecursiveAndDeleteCurrent_WhenModeIsPeak()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--recursive",
            "--delete-current",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.False(result.Arguments.Recursive);
        Assert.False(result.Arguments.DeleteCurrent);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(ConsoleTexts.RecursiveOption, StringComparison.Ordinal));
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(ConsoleTexts.DeleteCurrentOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldRejectSampleUnits_WhenModeIsPeak()
    {
        string[] args =
        [
            "--window-size", "2048samples",
            "--hop", "512samples",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "peak-analysis",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.SampleUnitOnlyForStftText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRequireTargetSampling_WhenSampleUnitIsUsedInStft()
    {
        string[] args =
        [
            "--window-size", "2048samples",
            "--hop", "512samples",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "stft-analysis",
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.TargetSamplingOption),
            result.Errors,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldAcceptSampleUnitsWithTargetSampling_WhenModeIsStft()
    {
        string[] args =
        [
            "--window-size", "2048samples",
            "--hop", "512sample",
            "--target-sampling", "44100hz",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "stft-analysis",
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(AnalysisLengthUnit.Sample, result.Arguments.WindowUnit);
        Assert.Equal(AnalysisLengthUnit.Sample, result.Arguments.HopUnit);
        Assert.Equal(2048, result.Arguments.WindowValue);
        Assert.Equal(512, result.Arguments.HopValue);
        Assert.Equal(44_100, result.Arguments.TargetSamplingHz);
    }

    [Fact]
    public void Parse_ShouldWarnAndIgnoreTargetSampling_WhenModeIsPeak()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--target-sampling", "44100hz",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "peak-analysis",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Null(result.Arguments.TargetSamplingHz);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(ConsoleTexts.TargetSamplingOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldRejectInvalidTargetSampling()
    {
        string[] args =
        [
            "--window-size", "2048samples",
            "--hop", "512samples",
            "--target-sampling", "44.1khz",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "stft-analysis",
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("Invalid sampling value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldUseStftDefaultTableName_WhenModeIsStft()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BaseStftArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(ConsoleTexts.DefaultStftTableName, result.Arguments.TableName, StringComparer.Ordinal);
        Assert.Equal(ConsoleTexts.StftAnalysisMode, result.Arguments.Mode, StringComparer.Ordinal);
        Assert.Equal(12, result.Arguments.BinCount);
    }

    [Fact]
    public void Parse_ShouldUsePeakDefaultTableName_WhenModeIsPeak()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BasePeakArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(ConsoleTexts.DefaultPeakTableName, result.Arguments.TableName, StringComparer.Ordinal);
        Assert.Equal(ConsoleTexts.PeakAnalysisMode, result.Arguments.Mode, StringComparer.Ordinal);
        Assert.Null(result.Arguments.BinCount);
    }

    [Fact]
    public void Parse_ShouldAcceptWindowsStylePaths()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", @"C:\audio\input",
            "--db-file", @"C:\data\analysis.db",
            "--ffmpeg-path", @"C:\tools\ffmpeg",
            "--mode", "peak-analysis",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(@"C:\audio\input", result.Arguments.InputDirectoryPath, StringComparer.Ordinal);
        Assert.Equal(@"C:\data\analysis.db", result.Arguments.DbFilePath, StringComparer.Ordinal);
        Assert.Equal(@"C:\tools\ffmpeg", result.Arguments.FfmpegPath, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldAcceptLinuxStylePaths()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", "/mnt/audio/input",
            "--db-file", "/var/tmp/analysis.db",
            "--ffmpeg-path", "/usr/bin",
            "--mode", "stft-analysis",
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal("/mnt/audio/input", result.Arguments.InputDirectoryPath, StringComparer.Ordinal);
        Assert.Equal("/var/tmp/analysis.db", result.Arguments.DbFilePath, StringComparer.Ordinal);
        Assert.Equal("/usr/bin", result.Arguments.FfmpegPath, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldEnableShowProgress_WhenShowProgressOptionIsSpecified()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--show-progress",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.True(result.Arguments.ShowProgress);
    }

    [Fact]
    public void Parse_ShouldRejectLegacyProgressOption()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--progress",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("--progress", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldApplyThreadAndQueueDefaults_WhenNotSpecified()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BaseStftArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(1, result.Arguments.StftProcThreads);
        Assert.Equal(1, result.Arguments.PeakProcThreads);
        Assert.Equal(1, result.Arguments.StftFileThreads);
        Assert.Equal(1, result.Arguments.PeakFileThreads);
        Assert.Equal(1024, result.Arguments.InsertQueueSize);
        Assert.False(result.Arguments.SqliteFastMode);
        Assert.Equal(512, result.Arguments.SqliteBatchRowCount);
    }

    [Fact]
    public void Parse_ShouldParseThreadAndQueueOptions()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--stft-proc-threads", "6",
            "--peak-proc-threads", "4",
            "--stft-file-threads", "3",
            "--peak-file-threads", "2",
            "--insert-queue-size", "4096",
            "--sqlite-fast-mode",
            "--sqlite-batch-row-count", "256",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(6, result.Arguments.StftProcThreads);
        Assert.Equal(4, result.Arguments.PeakProcThreads);
        Assert.Equal(3, result.Arguments.StftFileThreads);
        Assert.Equal(2, result.Arguments.PeakFileThreads);
        Assert.Equal(4096, result.Arguments.InsertQueueSize);
        Assert.True(result.Arguments.SqliteFastMode);
        Assert.Equal(256, result.Arguments.SqliteBatchRowCount);
    }

    [Fact]
    public void Parse_ShouldFail_WhenSqliteBatchRowCountIsInvalid()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--sqlite-batch-row-count", "0",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenSqliteBatchRowCountIsNegative()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--sqlite-batch-row-count", "-10",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenSqliteBatchRowCountIsNotNumeric()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--sqlite-batch-row-count", "abc",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldDefaultToSqliteBackend_WhenPostgresOptionIsNotSpecified()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BaseStftArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(StorageBackend.Sqlite, result.Arguments.StorageBackend);
        Assert.NotNull(result.Arguments.DbFilePath);
        Assert.Null(result.Arguments.PostgresHost);
    }

    [Fact]
    public void Parse_ShouldUsePostgresBackend_WhenPostgresOptionIsSpecified()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BasePostgresStftArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(StorageBackend.Postgres, result.Arguments.StorageBackend);
        Assert.Null(result.Arguments.DbFilePath);
        Assert.Equal("localhost", result.Arguments.PostgresHost, StringComparer.Ordinal);
        Assert.Equal(5432, result.Arguments.PostgresPort);
        Assert.Equal(1, result.Arguments.PostgresBatchRowCount);
        Assert.Equal("audio", result.Arguments.PostgresDatabase, StringComparer.Ordinal);
        Assert.Equal("analyzer", result.Arguments.PostgresUser, StringComparer.Ordinal);
        Assert.Contains(
            result.Warnings,
            warning => string.Equals(warning, ConsoleTexts.PostgresAuthNotProvidedWarningText, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldParsePostgresBatchRowCountOption_WhenPostgresMode()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-batch-row-count", "256",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(256, result.Arguments.PostgresBatchRowCount);
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresBatchRowCountIsInvalid()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-batch-row-count", "0",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresBatchRowCountIsNegative()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-batch-row-count", "-1",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresBatchRowCountIsNotNumeric()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-batch-row-count", "abc",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Invalid integer value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresRequiredOptionsAreMissing()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", "/mnt/audio/input",
            "--mode", "stft-analysis",
            "--bin-count", "12",
            "--postgres",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresHostOption, StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresPortOption, StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresDbOption, StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresUserOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresAndDbFileAreSpecifiedTogether()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--db-file", "C:/tmp/analysis.db",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.PostgresDbFileConflictText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldFail_WhenSqliteOptionsAreSpecifiedInPostgresMode()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--sqlite-fast-mode",
            "--sqlite-batch-row-count", "1024",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ConsoleTexts.SqliteFastModeOption, StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            error => error.Contains(ConsoleTexts.SqliteBatchRowCountOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresOnlyOptionsAreSpecifiedInSqliteMode()
    {
        string[] args =
        [
            .. BaseStftArgs,
            "--postgres-host", "localhost",
            "--postgres-batch-row-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Contains(ConsoleTexts.PostgresHostOption, StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            error => error.Contains(ConsoleTexts.PostgresBatchRowCountOption, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresPasswordAndClientCertAreSpecifiedTogether()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-password", "secret",
            "--postgres-sslcert-path", "/cert/client.crt",
            "--postgres-sslkey-path", "/cert/client.key",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.PostgresAuthConflictText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresSslCertAndKeyAreNotSpecifiedAsPair()
    {
        string[] onlyCert =
        [
            .. BasePostgresStftArgs,
            "--postgres-sslcert-path", "/cert/client.crt",
        ];

        CommandLineParseResult certResult = CommandLineParser.Parse(onlyCert);

        Assert.False(certResult.IsSuccess);
        Assert.Contains(ConsoleTexts.PostgresSslPairRequiredText, certResult.Errors, StringComparer.Ordinal);

        string[] onlyKey =
        [
            .. BasePostgresStftArgs,
            "--postgres-sslkey-path", "/cert/client.key",
        ];

        CommandLineParseResult keyResult = CommandLineParser.Parse(onlyKey);

        Assert.False(keyResult.IsSuccess);
        Assert.Contains(ConsoleTexts.PostgresSslPairRequiredText, keyResult.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldEnablePostgresSshAndApplyDefaultPort()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-password", "secret",
            "--postgres-ssh-host", "ssh-gw",
            "--postgres-ssh-user", "ubuntu",
            "--postgres-ssh-private-key-path", "/keys/id_ed25519",
            "--postgres-ssh-known-hosts-path", "/home/user/.ssh/known_hosts",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal("ssh-gw", result.Arguments.PostgresSshHost, StringComparer.Ordinal);
        Assert.Equal(22, result.Arguments.PostgresSshPort);
    }

    [Fact]
    public void Parse_ShouldFail_WhenPostgresSshHostIsSpecifiedWithoutRequiredSshOptions()
    {
        string[] args =
        [
            .. BasePostgresStftArgs,
            "--postgres-password", "secret",
            "--postgres-ssh-host", "ssh-gw",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresSshUserOption, StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresSshPrivateKeyPathOption, StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains(ConsoleTexts.PostgresSshKnownHostsPathOption, StringComparison.Ordinal));
    }
}
