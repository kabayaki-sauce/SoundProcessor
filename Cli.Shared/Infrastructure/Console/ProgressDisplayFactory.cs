using CliShared.Application.Ports;

namespace CliShared.Infrastructure.Console;

internal sealed class ProgressDisplayFactory : IProgressDisplayFactory
{
    public IProgressDisplay Create(bool enabled)
    {
        if (!enabled)
        {
            return NoOpProgressDisplay.Instance;
        }

        if (System.Console.IsErrorRedirected || !Environment.UserInteractive)
        {
            return NoOpProgressDisplay.Instance;
        }

        return new DualLineProgressDisplay(System.Console.Error);
    }
}
