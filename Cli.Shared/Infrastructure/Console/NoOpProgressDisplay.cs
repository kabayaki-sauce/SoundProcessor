using CliShared.Application.Models;
using CliShared.Application.Ports;

namespace CliShared.Infrastructure.Console;

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
