using Cli.Shared.Application.Models;
using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class NoOpProgressDisplay : IProgressDisplay
{
    public static NoOpProgressDisplay Instance { get; } = new();

    private NoOpProgressDisplay()
    {
    }

    public void Report(DualProgressState state)
    {
        _ = state;
    }

    public void Complete()
    {
    }
}
