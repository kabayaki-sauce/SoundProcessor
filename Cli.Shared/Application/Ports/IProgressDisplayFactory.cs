namespace Cli.Shared.Application.Ports;

public interface IProgressDisplayFactory
{
    public IProgressDisplay Create(bool enabled);
}
