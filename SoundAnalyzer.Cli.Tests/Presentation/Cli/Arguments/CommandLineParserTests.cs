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
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(6, result.Arguments.StftProcThreads);
        Assert.Equal(4, result.Arguments.PeakProcThreads);
        Assert.Equal(3, result.Arguments.StftFileThreads);
        Assert.Equal(2, result.Arguments.PeakFileThreads);
        Assert.Equal(4096, result.Arguments.InsertQueueSize);
    }
}
