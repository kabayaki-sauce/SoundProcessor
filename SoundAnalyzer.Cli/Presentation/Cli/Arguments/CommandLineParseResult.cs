namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal sealed class CommandLineParseResult
{
    private CommandLineParseResult(
        bool isSuccess,
        bool isHelpRequested,
        CommandLineArguments? arguments,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        IsSuccess = isSuccess;
        IsHelpRequested = isHelpRequested;
        Arguments = arguments;
        Errors = errors;
        Warnings = warnings;
    }

    public bool IsSuccess { get; }

    public bool IsHelpRequested { get; }

    public CommandLineArguments? Arguments { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<string> Warnings { get; }

    public static CommandLineParseResult Success(CommandLineArguments arguments, IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new CommandLineParseResult(
            true,
            false,
            arguments,
            Array.Empty<string>(),
            warnings ?? Array.Empty<string>());
    }

    public static CommandLineParseResult Help()
    {
        return new CommandLineParseResult(false, true, null, Array.Empty<string>(), Array.Empty<string>());
    }

    public static CommandLineParseResult Failure(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CommandLineParseResult(false, false, null, errors, Array.Empty<string>());
    }
}
