using AudioSplitter.Cli.Presentation.Cli.Arguments;
using AudioSplitter.Cli.Presentation.Cli.Texts;

namespace AudioSplitter.Cli.Tests.Presentation.Cli.Arguments;

public sealed class CommandLineParserTests
{
    private static readonly string[] BaseArgs =
    [
        "--output-dir", "C:/out",
        "--level", "-48.0",
        "--duration", "2000ms",
    ];

    [Fact]
    public void Parse_ShouldSucceed_WhenInputFileIsSpecified()
    {
        string[] args =
        [
            .. BaseArgs,
            "--input-file", "C:/input/file.wav",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Equal("C:/input/file.wav", result.Arguments.InputFilePath, StringComparer.Ordinal);
        Assert.Null(result.Arguments.InputDirectoryPath);
        Assert.False(result.Arguments.Recursive);
    }

    [Fact]
    public void Parse_ShouldSucceed_WhenInputDirAndRecursiveAreSpecified()
    {
        string[] args =
        [
            .. BaseArgs,
            "--input-dir", "C:/input",
            "--recursive",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.Null(result.Arguments.InputFilePath);
        Assert.Equal("C:/input", result.Arguments.InputDirectoryPath, StringComparer.Ordinal);
        Assert.True(result.Arguments.Recursive);
    }

    [Fact]
    public void Parse_ShouldFail_WhenInputFileAndInputDirAreSpecifiedTogether()
    {
        string[] args =
        [
            .. BaseArgs,
            "--input-file", "C:/input/file.wav",
            "--input-dir", "C:/input",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.InputSourceExclusiveText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldFail_WhenInputSourceIsNotSpecified()
    {
        string[] args = [.. BaseArgs];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.InputSourceRequiredText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldFail_WhenRecursiveIsSpecifiedWithoutInputDir()
    {
        string[] args =
        [
            .. BaseArgs,
            "--input-file", "C:/input/file.wav",
            "--recursive",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.IsSuccess);
        Assert.Contains(ConsoleTexts.RecursiveRequiresInputDirText, result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_ShouldEnableProgress_WhenProgressOptionIsSpecified()
    {
        string[] args =
        [
            .. BaseArgs,
            "--input-file", "C:/input/file.wav",
            "--progress",
        ];

        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Arguments);
        Assert.True(result.Arguments.Progress);
    }
}
