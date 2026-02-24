using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Infrastructure.FileSystem;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.FileSystem;

public sealed class SfftAudioFileResolverTests
{
    [Fact]
    public void Resolve_ShouldScanOnlyTopDirectoryFiles_WhenRecursiveIsFalse()
    {
        string root = CreateTempDirectory();
        try
        {
            string topFile = Path.Combine(root, "Top.wav");
            File.WriteAllText(topFile, "top");

            string subDirectory = Path.Combine(root, "Sub");
            Directory.CreateDirectory(subDirectory);
            File.WriteAllText(Path.Combine(subDirectory, "Nested.wav"), "nested");

            ResolvedSfftAudioFiles resolved = SfftAudioFileResolver.Resolve(root, recursive: false);

            Assert.Single(resolved.Files);
            Assert.Equal("Top", resolved.Files[0].Name, StringComparer.Ordinal);
            Assert.Equal(topFile, resolved.Files[0].FilePath, ignoreCase: true);
            Assert.Equal(1, resolved.DirectoryCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldScanRecursively_WhenRecursiveIsTrue()
    {
        string root = CreateTempDirectory();
        try
        {
            string topFile = Path.Combine(root, "Top.wav");
            File.WriteAllText(topFile, "top");

            string subDirectory = Path.Combine(root, "Sub");
            Directory.CreateDirectory(subDirectory);
            string nestedFile = Path.Combine(subDirectory, "Nested.flac");
            File.WriteAllText(nestedFile, "nested");

            ResolvedSfftAudioFiles resolved = SfftAudioFileResolver.Resolve(root, recursive: true);

            Assert.Equal(2, resolved.Files.Count);
            Assert.Contains(resolved.Files, file => string.Equals(file.Name, "Top", StringComparison.Ordinal));
            Assert.Contains(resolved.Files, file => string.Equals(file.Name, "Nested", StringComparison.Ordinal));
            Assert.True(resolved.DirectoryCount >= 2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldApplyExtensionPriority_ForSameBasenameInSameDirectory()
    {
        string root = CreateTempDirectory();
        try
        {
            string flacPath = Path.Combine(root, "Song.flac");
            string wavPath = Path.Combine(root, "Song.wav");
            File.WriteAllText(flacPath, "flac");
            File.WriteAllText(wavPath, "wav");

            ResolvedSfftAudioFiles resolved = SfftAudioFileResolver.Resolve(root, recursive: false);

            Assert.Single(resolved.Files);
            Assert.Equal("Song", resolved.Files[0].Name, StringComparer.Ordinal);
            Assert.Equal(wavPath, resolved.Files[0].FilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldFail_WhenDuplicateNamesExistAcrossInputSet_CaseInsensitive()
    {
        string root = CreateTempDirectory();
        try
        {
            string topPath = Path.Combine(root, "Song.wav");
            File.WriteAllText(topPath, "top");

            string subDirectory = Path.Combine(root, "Sub");
            Directory.CreateDirectory(subDirectory);
            string nestedPath = Path.Combine(subDirectory, "song.flac");
            File.WriteAllText(nestedPath, "nested");

            CliException exception = Assert.Throws<CliException>(
                () => SfftAudioFileResolver.Resolve(root, recursive: true));

            Assert.Equal(CliErrorCode.DuplicateSfftAnalysisName, exception.ErrorCode);
            Assert.Equal("song", exception.Detail, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sound-analyzer-sfft-resolver-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
