using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class TextBlockProgressDisplayFactory : ITextBlockProgressDisplayFactory
{
    private readonly IConsoleEnvironment consoleEnvironment;

    public TextBlockProgressDisplayFactory()
        : this(new ConsoleEnvironment())
    {
    }

    internal TextBlockProgressDisplayFactory(IConsoleEnvironment consoleEnvironment)
    {
        this.consoleEnvironment = consoleEnvironment ?? throw new ArgumentNullException(nameof(consoleEnvironment));
    }

    public ITextBlockProgressDisplay Create(bool enabled)
    {
        if (!enabled)
        {
            return NoOpTextBlockProgressDisplay.Instance;
        }

        if (consoleEnvironment.IsErrorRedirected || !consoleEnvironment.IsUserInteractive)
        {
            return NoOpTextBlockProgressDisplay.Instance;
        }

        consoleEnvironment.EnsureUtf8OutputEncoding();
        CursorControlMode cursorControlMode = ResolveCursorControlMode();
        return new TextBlockProgressDisplay(consoleEnvironment.ErrorWriter, cursorControlMode);
    }

    private static CursorControlMode ResolveCursorControlMode()
    {
        return IsAnsiEnabled()
            ? CursorControlMode.AnsiRelative
            : CursorControlMode.ConsoleAbsolute;
    }

    private static bool IsAnsiEnabled()
    {
        string? term = Environment.GetEnvironmentVariable("TERM");
        if (term is not null && term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
