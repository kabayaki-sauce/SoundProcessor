namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class StftAudioFile
{
    public StftAudioFile(string name, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Name = name;
        FilePath = filePath;
    }

    public string Name { get; }

    public string FilePath { get; }
}
