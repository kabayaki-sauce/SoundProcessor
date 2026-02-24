namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class ResolvedStemAudioFiles
{
    public ResolvedStemAudioFiles(IReadOnlyList<StemAudioFile> files, int directoryCount, int skippedStemCount)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedStemCount);

        Files = files;
        DirectoryCount = directoryCount;
        SkippedStemCount = skippedStemCount;
    }

    public IReadOnlyList<StemAudioFile> Files { get; }

    public int DirectoryCount { get; }

    public int SkippedStemCount { get; }
}
