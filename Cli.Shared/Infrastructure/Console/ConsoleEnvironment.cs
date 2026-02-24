using System.Text;

namespace Cli.Shared.Infrastructure.Console;

internal sealed class ConsoleEnvironment : IConsoleEnvironment
{
    public bool IsErrorRedirected => System.Console.IsErrorRedirected;

    public bool IsUserInteractive => Environment.UserInteractive;

    public void EnsureUtf8OutputEncoding()
    {
        System.Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    public TextWriter ErrorWriter => System.Console.Error;
}
