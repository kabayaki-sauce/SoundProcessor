namespace Cli.Shared.Infrastructure.Console;

internal interface IConsoleEnvironment
{
    public bool IsErrorRedirected { get; }

    public bool IsUserInteractive { get; }

    public void EnsureUtf8OutputEncoding();

    public TextWriter ErrorWriter { get; }
}
