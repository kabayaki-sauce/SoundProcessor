using AudioSplitter.Core.Domain.ValueObjects;

namespace AudioSplitter.Cli.Presentation.Cli.Arguments;

internal sealed class CommandLineArguments
{
    public CommandLineArguments(
        string? inputFilePath,
        string? inputDirectoryPath,
        string outputDirectoryPath,
        double levelDb,
        TimeSpan duration,
        TimeSpan afterOffset,
        TimeSpan resumeOffset,
        ResolutionType? resolutionType,
        string? ffmpegPath,
        bool overwriteWithoutPrompt,
        bool recursive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);

        bool hasInputFile = !string.IsNullOrWhiteSpace(inputFilePath);
        bool hasInputDirectory = !string.IsNullOrWhiteSpace(inputDirectoryPath);
        if (hasInputFile == hasInputDirectory)
        {
            throw new ArgumentException("Exactly one of inputFilePath or inputDirectoryPath must be specified.");
        }

        if (recursive && !hasInputDirectory)
        {
            throw new ArgumentException("recursive requires inputDirectoryPath.");
        }

        InputFilePath = hasInputFile ? inputFilePath : null;
        InputDirectoryPath = hasInputDirectory ? inputDirectoryPath : null;
        OutputDirectoryPath = outputDirectoryPath;
        LevelDb = levelDb;
        Duration = duration;
        AfterOffset = afterOffset;
        ResumeOffset = resumeOffset;
        ResolutionType = resolutionType;
        FfmpegPath = ffmpegPath;
        OverwriteWithoutPrompt = overwriteWithoutPrompt;
        Recursive = recursive;
    }

    public string? InputFilePath { get; }

    public string? InputDirectoryPath { get; }

    public string OutputDirectoryPath { get; }

    public double LevelDb { get; }

    public TimeSpan Duration { get; }

    public TimeSpan AfterOffset { get; }

    public TimeSpan ResumeOffset { get; }

    public ResolutionType? ResolutionType { get; }

    public string? FfmpegPath { get; }

    public bool OverwriteWithoutPrompt { get; }

    public bool Recursive { get; }
}
