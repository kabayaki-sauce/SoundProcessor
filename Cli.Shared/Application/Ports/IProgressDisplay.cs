using CliShared.Application.Models;

namespace CliShared.Application.Ports;

public interface IProgressDisplay
{
    public void Report(DualProgressState state);

    public void Complete();
}
