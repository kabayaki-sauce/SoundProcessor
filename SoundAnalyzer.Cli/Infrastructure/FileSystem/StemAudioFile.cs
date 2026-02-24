namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class StemAudioFile
{
    public StemAudioFile(string name, string stem, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Name = name;
        Stem = stem;
        FilePath = filePath;
    }

    public string Name { get; }

    public string Stem { get; }

    public string FilePath { get; }
}
