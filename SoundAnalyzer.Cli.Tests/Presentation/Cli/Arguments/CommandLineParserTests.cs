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

    private static readonly string[] BaseSfftArgs =
    [
        "--window-size", "50ms",
        "--hop", "10ms",
        "--input-dir", "C:/input",
        "--db-file", "C:/tmp/analysis.db",
        "--mode", "sfft-analysis",
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
    public void Parse_ShouldRequireBinCount_WhenModeIsSfft()
    {
        string[] args =
        [
            "--window-size", "50ms",
            "--hop", "10ms",
            "--input-dir", "C:/input",
            "--db-file", "C:/tmp/analysis.db",
            "--mode", "sfft-analysis",
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
    public void Parse_ShouldRejectStems_WhenModeIsSfft()
    {
        string[] args =
        [
            .. BaseSfftArgs,
            "--stems", "Piano",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.StemsNotSupportedForSfftText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRejectBinCount_WhenModeIsPeak()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--bin-count", "12",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.BinCountOnlyForSfftText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRejectRecursive_WhenModeIsPeak()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--recursive",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.RecursiveOnlyForSfftText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRejectDeleteCurrent_WhenModeIsPeak()
    {
        string[] args =
        [
            .. BasePeakArgs,
            "--delete-current",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.DeleteCurrentOnlyForSfftText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldUseSfftDefaultTableName_WhenModeIsSfft()
    {
        CommandLineParseResult result = CommandLineParser.Parse(BaseSfftArgs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal(ConsoleTexts.DefaultSfftTableName, result.Arguments.TableName, StringComparer.Ordinal);
        Assert.Equal(ConsoleTexts.SfftAnalysisMode, result.Arguments.Mode, StringComparer.Ordinal);
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
    public void Parse_ShouldEnableProgress_WhenProgressOptionIsSpecified()
    {
        string[] args =
        [
            .. BaseSfftArgs,
            "--progress",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.True(result.Arguments.Progress);
    }
}
