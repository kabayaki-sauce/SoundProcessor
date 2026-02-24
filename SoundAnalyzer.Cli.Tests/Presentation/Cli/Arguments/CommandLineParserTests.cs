using SoundAnalyzer.Cli.Presentation.Cli.Arguments;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Tests.Presentation.Cli.Arguments;

public sealed class CommandLineParserTests
{
    private static readonly string[] BaseArgs =
    [
        "--window-size", "50ms",
        "--hop", "10ms",
        "--input-dir", "C:/input",
        "--db-file", "C:/tmp/analysis.db",
        "--mode", "peak-analysis",
    ];

    [Fact]
    public void Parse_ShouldFail_WhenUpsertAndSkipDuplicateAreSpecifiedTogether()
    {
        string[] args =
        [
            .. BaseArgs,
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
            .. BaseArgs,
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
            .. BaseArgs,
            "--table-name-override", "T PEAK",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("Invalid table name", StringComparison.Ordinal));
    }
}
