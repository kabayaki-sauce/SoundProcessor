using Cli.Shared.Application.Models;

namespace Cli.Shared.Application.Ports;

public interface IProgressDisplay
{
    public void Report(DualProgressState state);

    public void Complete();
}
