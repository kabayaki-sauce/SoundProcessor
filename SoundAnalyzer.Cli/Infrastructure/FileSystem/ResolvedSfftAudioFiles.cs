namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class ResolvedSfftAudioFiles
{
    public ResolvedSfftAudioFiles(IReadOnlyList<SfftAudioFile> files, int directoryCount)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);

        Files = files;
        DirectoryCount = directoryCount;
    }

    public IReadOnlyList<SfftAudioFile> Files { get; }

    public int DirectoryCount { get; }
}
