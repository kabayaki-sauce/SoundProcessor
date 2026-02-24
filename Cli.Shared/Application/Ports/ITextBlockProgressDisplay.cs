namespace Cli.Shared.Application.Ports;

public interface ITextBlockProgressDisplay
{
    public void Report(IReadOnlyList<string> lines, bool force = false);

    public void Complete();
}
