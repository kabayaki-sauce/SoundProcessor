using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class NoOpTextBlockProgressDisplay : ITextBlockProgressDisplay
{
    public static NoOpTextBlockProgressDisplay Instance { get; } = new();

    private NoOpTextBlockProgressDisplay()
    {
    }

    public void Report(IReadOnlyList<string> lines, bool force = false)
    {
        _ = lines;
        _ = force;
    }

    public void Complete()
    {
    }
}
