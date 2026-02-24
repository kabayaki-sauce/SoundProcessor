namespace Cli.Shared.Infrastructure.Console;

internal sealed class ConsoleEnvironment : IConsoleEnvironment
{
    public bool IsErrorRedirected => System.Console.IsErrorRedirected;

    public bool IsUserInteractive => Environment.UserInteractive;

    public TextWriter ErrorWriter => System.Console.Error;
}
