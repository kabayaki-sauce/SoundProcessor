namespace CliShared.Infrastructure.Console;

internal interface IConsoleEnvironment
{
    public bool IsErrorRedirected { get; }

    public bool IsUserInteractive { get; }

    public TextWriter ErrorWriter { get; }
}
