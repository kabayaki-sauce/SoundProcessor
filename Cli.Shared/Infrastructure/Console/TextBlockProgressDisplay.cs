using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class TextBlockProgressDisplay : ITextBlockProgressDisplay
{
    private static readonly TimeSpan MinRenderInterval = TimeSpan.FromMilliseconds(80);

    private readonly object sync = new();
    private readonly ProgressBlockRenderer renderer;
    private DateTimeOffset lastRenderAt;
    private string[] latestLines = Array.Empty<string>();

    public TextBlockProgressDisplay(TextWriter writer)
        : this(writer, CursorControlMode.Disabled)
    {
    }

    internal TextBlockProgressDisplay(TextWriter writer, CursorControlMode cursorControlMode)
    {
        renderer = new ProgressBlockRenderer(writer ?? throw new ArgumentNullException(nameof(writer)), cursorControlMode);
    }

    public void Report(IReadOnlyList<string> lines, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(lines);

        lock (sync)
        {
            latestLines = lines.Count == 0 ? Array.Empty<string>() : lines.ToArray();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!force && now - lastRenderAt < MinRenderInterval)
            {
                return;
            }

            renderer.Render(latestLines);
            lastRenderAt = now;
        }
    }

    public void Complete()
    {
        lock (sync)
        {
            if (latestLines.Length > 0)
            {
                renderer.Render(latestLines);
            }

            renderer.Complete();
        }
    }
}
