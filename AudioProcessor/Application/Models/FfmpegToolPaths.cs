namespace AudioProcessor.Application.Models;

public sealed class FfmpegToolPaths
{
    public FfmpegToolPaths(string ffmpegPath, string ffprobePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobePath);

        FfmpegPath = ffmpegPath;
        FfprobePath = ffprobePath;
    }

    public string FfmpegPath { get; }

    public string FfprobePath { get; }
}
