using System.Globalization;
using System.Text;
using CliShared.Application.Models;
using CliShared.Application.Ports;

namespace CliShared.Infrastructure.Console;

internal sealed class DualLineProgressDisplay : IProgressDisplay
{
    private const int BarWidth = 36;
    private static readonly TimeSpan MinRenderInterval = TimeSpan.FromMilliseconds(80);

    private readonly object sync = new();
    private readonly TextWriter writer;

    private int originTop = -1;
    private int renderedLineCount;
    private DateTimeOffset lastRenderAt;
    private DualProgressState? latestState;

    public DualLineProgressDisplay(TextWriter writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        System.Console.OutputEncoding = Encoding.UTF8;
    }

    public void Report(DualProgressState state)
    {
        lock (sync)
        {
            latestState = state;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool isCompletion = IsCompletion(state);
            if (!isCompletion && now - lastRenderAt < MinRenderInterval)
            {
                return;
            }

            RenderCore(state);
            lastRenderAt = now;
        }
    }

    public void Complete()
    {
        lock (sync)
        {
            if (latestState.HasValue)
            {
                RenderCore(latestState.Value);
            }

            if (renderedLineCount > 0 && originTop >= 0)
            {
                TrySetCursorPosition(0, originTop + renderedLineCount);
            }
        }
    }

    private void RenderCore(DualProgressState state)
    {
        string topLine = BuildLine(state.TopLabel, state.TopRatio);
        string bottomLine = BuildLine(state.BottomLabel, state.BottomRatio);

        if (originTop < 0)
        {
            originTop = GetCurrentCursorTop();
        }
        else
        {
            TrySetCursorPosition(0, originTop);
        }

        int width = ResolveWidth();
        WriteLineFixed(topLine, width);
        WriteLineFixed(bottomLine, width);
        renderedLineCount = 2;
    }

    private void WriteLineFixed(string text, int width)
    {
        string safeText = text.Length <= width
            ? text
            : text[..width];

        writer.Write(safeText);
        if (width > safeText.Length)
        {
            writer.Write(new string(' ', width - safeText.Length));
        }

        writer.WriteLine();
        writer.Flush();
    }

    private static string BuildLine(string label, double ratio)
    {
        double clamped = ClampRatio(ratio);
        int filled = checked((int)Math.Round(clamped * BarWidth, MidpointRounding.AwayFromZero));
        if (filled > BarWidth)
        {
            filled = BarWidth;
        }

        char[] chars = new char[BarWidth];
        for (int i = 0; i < BarWidth; i++)
        {
            chars[i] = i < filled ? '█' : '░';
        }

        string bar = new(chars);
        string percent = (clamped * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label,-28} |{bar}| {percent,6}%");
    }

    private static bool IsCompletion(DualProgressState state)
    {
        return ClampRatio(state.TopRatio) >= 1.0 || ClampRatio(state.BottomRatio) >= 1.0;
    }

    private static double ClampRatio(double ratio)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return 0;
        }

        if (ratio <= 0)
        {
            return 0;
        }

        if (ratio >= 1)
        {
            return 1;
        }

        return ratio;
    }

    private static int ResolveWidth()
    {
        try
        {
            return Math.Max(40, System.Console.BufferWidth - 1);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 120;
        }
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

    private void TrySetCursorPosition(int left, int top)
    {
        try
        {
            System.Console.SetCursorPosition(left, top);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            originTop = GetCurrentCursorTop();
        }
    }
}
