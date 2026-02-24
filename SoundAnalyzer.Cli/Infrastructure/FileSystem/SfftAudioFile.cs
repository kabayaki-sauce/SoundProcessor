namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class SfftAudioFile
{
    public SfftAudioFile(string name, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Name = name;
        FilePath = filePath;
    }

    public string Name { get; }

    public string FilePath { get; }
}
