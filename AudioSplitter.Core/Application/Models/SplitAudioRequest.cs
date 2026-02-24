using AudioSplitter.Core.Domain.ValueObjects;

namespace AudioSplitter.Core.Application.Models;

public sealed class SplitAudioRequest
{
    public SplitAudioRequest(
        string inputFilePath,
        string outputDirectoryPath,
        double levelDb,
        TimeSpan duration,
        TimeSpan afterOffset,
        TimeSpan resumeOffset,
        ResolutionType? resolutionType,
        string? ffmpegPath,
        bool overwriteWithoutPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);

        InputFilePath = inputFilePath;
        OutputDirectoryPath = outputDirectoryPath;
        LevelDb = levelDb;
        Duration = duration;
        AfterOffset = afterOffset;
        ResumeOffset = resumeOffset;
        ResolutionType = resolutionType;
        FfmpegPath = ffmpegPath;
        OverwriteWithoutPrompt = overwriteWithoutPrompt;
    }

    public string InputFilePath { get; }

    public string OutputDirectoryPath { get; }

    public double LevelDb { get; }

    public TimeSpan Duration { get; }

    public TimeSpan AfterOffset { get; }

    public TimeSpan ResumeOffset { get; }

    public ResolutionType? ResolutionType { get; }

    public string? FfmpegPath { get; }

    public bool OverwriteWithoutPrompt { get; }
}
