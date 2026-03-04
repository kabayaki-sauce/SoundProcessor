namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal sealed class ResolvedMelSpectrogramAudioFiles
{
    public ResolvedMelSpectrogramAudioFiles(IReadOnlyList<MelSpectrogramAudioFile> files, int directoryCount)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(directoryCount);

        Files = files;
        DirectoryCount = directoryCount;
    }

    public IReadOnlyList<MelSpectrogramAudioFile> Files { get; }

    public int DirectoryCount { get; }
}

