namespace Cli.Shared.Application.Ports;

public interface ITextBlockProgressDisplayFactory
{
    public ITextBlockProgressDisplay Create(bool enabled);
}
