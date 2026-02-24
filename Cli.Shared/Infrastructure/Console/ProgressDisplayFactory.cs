using Cli.Shared.Application.Ports;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class ProgressDisplayFactory : IProgressDisplayFactory
{
    private readonly IConsoleEnvironment consoleEnvironment;

    public ProgressDisplayFactory()
        : this(new ConsoleEnvironment())
    {
    }

    internal ProgressDisplayFactory(IConsoleEnvironment consoleEnvironment)
    {
        this.consoleEnvironment = consoleEnvironment ?? throw new ArgumentNullException(nameof(consoleEnvironment));
    }

    public IProgressDisplay Create(bool enabled)
    {
        if (!enabled)
        {
            return NoOpProgressDisplay.Instance;
        }

        if (consoleEnvironment.IsErrorRedirected || !consoleEnvironment.IsUserInteractive)
        {
            return NoOpProgressDisplay.Instance;
        }

        return new DualLineProgressDisplay(consoleEnvironment.ErrorWriter);
    }
}
