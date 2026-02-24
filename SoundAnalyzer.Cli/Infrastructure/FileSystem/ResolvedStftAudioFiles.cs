namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class ResolvedStftAudioFiles
{
    public ResolvedStftAudioFiles(IReadOnlyList<StftAudioFile> files, int directoryCount)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);

        Files = files;
        DirectoryCount = directoryCount;
    }

    public IReadOnlyList<StftAudioFile> Files { get; }

    public int DirectoryCount { get; }
}
