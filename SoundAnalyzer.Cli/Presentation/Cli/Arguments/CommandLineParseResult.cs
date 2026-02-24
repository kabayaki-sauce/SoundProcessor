namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal sealed class CommandLineParseResult
{
    private CommandLineParseResult(
        bool isSuccess,
        bool isHelpRequested,
        CommandLineArguments? arguments,
        IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        IsSuccess = isSuccess;
        IsHelpRequested = isHelpRequested;
        Arguments = arguments;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsHelpRequested { get; }

    public CommandLineArguments? Arguments { get; }

    public IReadOnlyList<string> Errors { get; }

    public static CommandLineParseResult Success(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new CommandLineParseResult(true, false, arguments, Array.Empty<string>());
    }

    public static CommandLineParseResult Help()
    {
        return new CommandLineParseResult(false, true, null, Array.Empty<string>());
    }

    public static CommandLineParseResult Failure(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CommandLineParseResult(false, false, null, errors);
    }
}
