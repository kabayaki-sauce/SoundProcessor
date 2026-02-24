using System.Globalization;
using System.Text;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class ProgressBlockRenderer
{
    private readonly TextWriter writer;
    private CursorControlMode cursorControlMode;
    private int originTop = -1;
    private int renderedLineCount;

    public ProgressBlockRenderer(TextWriter writer, CursorControlMode cursorControlMode)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.cursorControlMode = cursorControlMode;
    }

    public void Render(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        switch (cursorControlMode)
        {
            case CursorControlMode.AnsiRelative:
                RenderWithAnsiRelativeCursor(lines);
                break;
            case CursorControlMode.ConsoleAbsolute:
                RenderWithConsoleAbsoluteCursor(lines);
                break;
            default:
                RenderAppendOnly(lines);
                break;
        }
    }

    public void Complete()
    {
        if (renderedLineCount <= 0)
        {
            return;
        }

        switch (cursorControlMode)
        {
            case CursorControlMode.AnsiRelative:
                writer.WriteLine();
                writer.Flush();
                break;
            case CursorControlMode.ConsoleAbsolute:
                if (originTop >= 0)
                {
                    _ = TrySetCursorPosition(0, originTop + renderedLineCount);
                }

                break;
            default:
                break;
        }
    }

    private void RenderWithAnsiRelativeCursor(IReadOnlyList<string> lines)
    {
        int width = ResolveWidth();
        int rows = Math.Max(renderedLineCount, lines.Count);
        if (renderedLineCount > 0)
        {
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b[{renderedLineCount}A"));
        }

        for (int i = 0; i < rows; i++)
        {
            writer.Write("\u001b[2K\r");
            if (i < lines.Count)
            {
                WriteLineFixed(lines[i], width);
            }
            else
            {
                WriteLineFixed(string.Empty, width);
            }
        }

        renderedLineCount = lines.Count;
    }

    private void RenderWithConsoleAbsoluteCursor(IReadOnlyList<string> lines)
    {
        if (originTop < 0)
        {
            originTop = GetCurrentCursorTop();
        }
        else
        {
            bool moved = TrySetCursorPosition(0, originTop);
            if (!moved)
            {
                cursorControlMode = CursorControlMode.AnsiRelative;
                RenderWithAnsiRelativeCursor(lines);
                return;
            }
        }

        int width = ResolveWidth();
        for (int i = 0; i < lines.Count; i++)
        {
            WriteLineFixed(lines[i], width);
        }

        if (renderedLineCount > lines.Count)
        {
            for (int i = lines.Count; i < renderedLineCount; i++)
            {
                WriteLineFixed(string.Empty, width);
            }
        }

        renderedLineCount = lines.Count;
    }

    private void RenderAppendOnly(IReadOnlyList<string> lines)
    {
        int width = ResolveWidth();
        for (int i = 0; i < lines.Count; i++)
        {
            WriteLineFixed(lines[i], width);
        }

        renderedLineCount = lines.Count;
    }

    private void WriteLineFixed(string text, int width)
    {
        string safeText = TruncateToDisplayWidth(text, width);
        writer.Write(safeText);
        int displayLength = GetDisplayLength(safeText);
        if (displayLength < width)
        {
            writer.Write(new string(' ', width - displayLength));
        }

        writer.WriteLine();
        writer.Flush();
    }

    private static string TruncateToDisplayWidth(string text, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (width <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(capacity: text.Length);
        int displayLength = 0;
        int index = 0;
        bool styleActive = false;

        while (index < text.Length && displayLength < width)
        {
            if (text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
            {
                if (!TryReadAnsiEscape(text, index, out int escapeEnd, out string? code))
                {
                    break;
                }

                builder.Append(text, index, escapeEnd - index + 1);
                styleActive = code is not null && !string.Equals(code, "0", StringComparison.Ordinal);
                index = escapeEnd + 1;
                continue;
            }

            builder.Append(text[index]);
            displayLength++;
            index++;
        }

        while (index < text.Length && text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
        {
            if (!TryReadAnsiEscape(text, index, out int escapeEnd, out string? code))
            {
                break;
            }

            builder.Append(text, index, escapeEnd - index + 1);
            styleActive = code is not null && !string.Equals(code, "0", StringComparison.Ordinal);
            index = escapeEnd + 1;
        }

        if (styleActive)
        {
            builder.Append("\u001b[0m");
        }

        return builder.ToString();
    }

    private static bool TryReadAnsiEscape(string text, int index, out int escapeEnd, out string? code)
    {
        escapeEnd = index;
        code = null;

        int cursor = index + 2;
        while (cursor < text.Length && text[cursor] != 'm')
        {
            cursor++;
        }

        if (cursor >= text.Length)
        {
            return false;
        }

        code = text[(index + 2)..cursor];
        escapeEnd = cursor;
        return true;
    }

    private static int GetDisplayLength(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int displayLength = 0;
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
            {
                int escapeTail = index + 2;
                while (escapeTail < text.Length && text[escapeTail] != 'm')
                {
                    escapeTail++;
                }

                index = escapeTail < text.Length ? escapeTail + 1 : text.Length;
                continue;
            }

            displayLength++;
            index++;
        }

        return displayLength;
    }

    private static int ResolveWidth()
    {
        int? bufferWidth = null;
        try
        {
            bufferWidth = System.Console.BufferWidth;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            bufferWidth = null;
        }

        string? columnsText = Environment.GetEnvironmentVariable("COLUMNS");
        return ResolveWidthFromSnapshot(bufferWidth, columnsText);
    }

    internal static int ResolveWidthFromSnapshot(int? bufferWidth, string? columnsText)
    {
        if (bufferWidth is > 1)
        {
            return Math.Max(60, bufferWidth.Value - 1);
        }

        bool parsed = int.TryParse(
            columnsText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int parsedColumns);
        if (parsed && parsedColumns > 1)
        {
            return Math.Max(60, parsedColumns - 1);
        }

        return 80;
    }

    private static int GetCurrentCursorTop()
    {
        try
        {
            return System.Console.CursorTop;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 0;
        }
    }

    private static bool TrySetCursorPosition(int left, int top)
    {
        try
        {
            System.Console.SetCursorPosition(left, top);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
