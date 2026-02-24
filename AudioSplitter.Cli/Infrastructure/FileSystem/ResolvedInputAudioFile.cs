namespace AudioSplitter.Cli.Infrastructure.FileSystem;

internal sealed class ResolvedInputAudioFile
{
    public ResolvedInputAudioFile(string inputFilePath, string relativeDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentNullException.ThrowIfNull(relativeDirectoryPath);

        InputFilePath = inputFilePath;
        RelativeDirectoryPath = relativeDirectoryPath;
    }

    public string InputFilePath { get; }

    public string RelativeDirectoryPath { get; }
}
