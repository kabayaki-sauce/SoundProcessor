using Cli.Shared.Infrastructure.Console;

namespace Cli.Shared.Tests.Infrastructure.Console;

public sealed class TextBlockProgressDisplayTests
{
    [Fact]
    public void Report_ShouldUseRelativeCursorSequences_WhenModeIsAnsiRelative()
    {
        StringWriter writer = new();
        TextBlockProgressDisplay display = new(writer, CursorControlMode.AnsiRelative);

        display.Report(["Line-1", "Line-2"], force: true);
        display.Report(["Line-3", "Line-4"], force: true);

        string text = writer.ToString();
        Assert.Contains("\u001b[2A", text, StringComparison.Ordinal);
        Assert.Contains("\u001b[2K\r", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_ShouldClearExtraRows_WhenLineCountDecreases()
    {
        StringWriter writer = new();
        TextBlockProgressDisplay display = new(writer, CursorControlMode.AnsiRelative);

        display.Report(["L1", "L2", "L3"], force: true);
        display.Report(["L1"], force: true);

        string text = writer.ToString();
        Assert.Contains("\u001b[3A", text, StringComparison.Ordinal);
        Assert.True(CountSubstring(text, "\u001b[2K\r") >= 4);
    }

    [Fact]
    public void Report_ShouldPreserveAnsiTermination_WhenTruncated()
    {
        StringWriter writer = new();
        ProgressBlockRenderer renderer = new(writer, CursorControlMode.AnsiRelative);
        string longGreenLine = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"\u001b[32m{new string('X', 300)}");

        renderer.Render([longGreenLine]);

        string text = writer.ToString();
        Assert.Contains("\u001b[0m", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ShouldRenderLatestLinesAgain()
    {
        StringWriter writer = new();
        TextBlockProgressDisplay display = new(writer, CursorControlMode.Disabled);

        display.Report(["A", "B"], force: true);
        int linesAfterReport = CountNonEmptyLines(writer.ToString());

        display.Complete();
        int linesAfterComplete = CountNonEmptyLines(writer.ToString());

        Assert.True(linesAfterReport >= 2);
        Assert.True(linesAfterComplete >= linesAfterReport + 2);
    }

    private static int CountSubstring(string source, string value)
    {
        int count = 0;
        int index = 0;
        while (index >= 0)
        {
            index = source.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            index += value.Length;
        }

        return count;
    }

    private static int CountNonEmptyLines(string text)
    {
        return text
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Count(line => !string.IsNullOrEmpty(line));
    }
}
