using Cli.Shared.Application.Models;
using Cli.Shared.Infrastructure.Console;

namespace Cli.Shared.Tests.Infrastructure.Console;

public sealed class DualLineProgressDisplayTests
{
    [Fact]
    public void Report_ShouldWriteTwoLines_WithPercentText()
    {
        StringWriter writer = new();
        DualLineProgressDisplay display = new(writer);

        display.Report(new DualProgressState("Songs", 0.5, "Current", 0.25));

        string text = writer.ToString();
        Assert.Contains("Songs", text, StringComparison.Ordinal);
        Assert.Contains("Current", text, StringComparison.Ordinal);
        Assert.Contains("50.0%", text, StringComparison.Ordinal);
        Assert.Contains("25.0%", text, StringComparison.Ordinal);
        Assert.Contains('█', text);
        Assert.Contains('░', text);
    }

    [Fact]
    public void Report_ShouldClampOutOfRangeRatios()
    {
        StringWriter writer = new();
        DualLineProgressDisplay display = new(writer);

        display.Report(new DualProgressState("Top", -5, "Bottom", 2));

        string text = writer.ToString();
        Assert.Contains("0.0%", text, StringComparison.Ordinal);
        Assert.Contains("100.0%", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderLatestStateAgain()
    {
        StringWriter writer = new();
        DualLineProgressDisplay display = new(writer);

        display.Report(new DualProgressState("Top", 0.4, "Bottom", 0.4));
        int linesAfterReport = CountLines(writer.ToString());

        display.Complete();
        int linesAfterComplete = CountLines(writer.ToString());

        Assert.True(linesAfterReport >= 2);
        Assert.True(linesAfterComplete >= linesAfterReport + 2);
    }

    [Fact]
    public void Report_ShouldBypassThrottle_OnCompletionUpdate()
    {
        StringWriter writer = new();
        DualLineProgressDisplay display = new(writer);

        display.Report(new DualProgressState("Top", 0.1, "Bottom", 0.1));
        int linesAfterFirst = CountLines(writer.ToString());

        display.Report(new DualProgressState("Top", 1.0, "Bottom", 1.0));
        int linesAfterSecond = CountLines(writer.ToString());

        Assert.True(linesAfterFirst >= 2);
        Assert.True(linesAfterSecond >= linesAfterFirst + 2);
    }

    private static int CountLines(string text)
    {
        return text
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Count(line => !string.IsNullOrEmpty(line));
    }
}
