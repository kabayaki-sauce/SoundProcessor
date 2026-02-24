using System.Globalization;
using Cli.Shared.Application.Models;
using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class DualLineProgressDisplay : IProgressDisplay
{
    private const int BarWidth = 36;
    private static readonly TimeSpan MinRenderInterval = TimeSpan.FromMilliseconds(80);

    private readonly object sync = new();
    private readonly ProgressBlockRenderer renderer;
    private DateTimeOffset lastRenderAt;
    private DualProgressState? latestState;

    public DualLineProgressDisplay(TextWriter writer)
        : this(writer, CursorControlMode.Disabled)
    {
    }

    internal DualLineProgressDisplay(TextWriter writer, CursorControlMode cursorControlMode)
    {
        renderer = new ProgressBlockRenderer(writer ?? throw new ArgumentNullException(nameof(writer)), cursorControlMode);
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

            renderer.Complete();
        }
    }

    private void RenderCore(DualProgressState state)
    {
        string topLine = BuildLine(state.TopLabel, state.TopRatio);
        string bottomLine = BuildLine(state.BottomLabel, state.BottomRatio);
        renderer.Render([topLine, bottomLine]);
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

}
