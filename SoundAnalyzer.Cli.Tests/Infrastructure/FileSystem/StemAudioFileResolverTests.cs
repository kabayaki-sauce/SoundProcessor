using SoundAnalyzer.Cli.Infrastructure.FileSystem;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.FileSystem;

public sealed class StemAudioFileResolverTests
{
    private static readonly string[] PianoStem = ["piano"];
    private static readonly string[] PianoStemPascal = ["Piano"];

    [Fact]
    public void Resolve_ShouldScanOnlyFirstLevelSubdirectories()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "Root.wav"), "root");

            string firstLevel = Path.Combine(root, "AlbumA");
            Directory.CreateDirectory(firstLevel);
            string selectedFile = Path.Combine(firstLevel, "Piano.wav");
            File.WriteAllText(selectedFile, "piano");

            string nested = Path.Combine(firstLevel, "Nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "Drums.wav"), "drums");

            ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(root, stems: null);

            Assert.Single(resolved.Files);
            Assert.Equal("AlbumA", resolved.Files[0].Name);
            Assert.Equal("Piano", resolved.Files[0].Stem);
            Assert.Equal(selectedFile, resolved.Files[0].FilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldMatchStemCaseInsensitively()
    {
        string root = CreateTempDirectory();
        try
        {
            string dir = Path.Combine(root, "AlbumA");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, "PIANO.WAV");
            File.WriteAllText(filePath, "piano");

            ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(root, PianoStem);

            Assert.Single(resolved.Files);
            Assert.Equal("PIANO", resolved.Files[0].Stem);
            Assert.Equal(filePath, resolved.Files[0].FilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldApplyExtensionPriority()
    {
        string root = CreateTempDirectory();
        try
        {
            string dir = Path.Combine(root, "AlbumA");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Piano.flac"), "flac");
            File.WriteAllText(Path.Combine(dir, "Piano.m4a"), "alac");
            string expected = Path.Combine(dir, "Piano.wav");
            File.WriteAllText(expected, "wav");

            ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(root, PianoStemPascal);

            Assert.Single(resolved.Files);
            Assert.Equal(expected, resolved.Files[0].FilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldUseDirectoryEnumerationOrderForOtherExtensions()
    {
        string root = CreateTempDirectory();
        try
        {
            string dir = Path.Combine(root, "AlbumA");
            Directory.CreateDirectory(dir);
            string first = Path.Combine(dir, "Piano.zzz");
            string second = Path.Combine(dir, "Piano.aaa");
            File.WriteAllText(first, "a");
            File.WriteAllText(second, "b");

            string expected = Directory
                .EnumerateFiles(dir, "Piano.*", SearchOption.TopDirectoryOnly)
                .First();

            ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(root, PianoStemPascal);

            Assert.Single(resolved.Files);
            Assert.Equal(expected, resolved.Files[0].FilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldAnalyzeAllStems_WhenStemsOptionIsMissing()
    {
        string root = CreateTempDirectory();
        try
        {
            string dir = Path.Combine(root, "AlbumA");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Piano.wav"), "piano");
            File.WriteAllText(Path.Combine(dir, "Drums.flac"), "drums");

            ResolvedStemAudioFiles resolved = StemAudioFileResolver.Resolve(root, stems: null);

            Assert.Equal(2, resolved.Files.Count);
            Assert.Contains(resolved.Files, file => string.Equals(file.Stem, "Piano", StringComparison.Ordinal));
            Assert.Contains(resolved.Files, file => string.Equals(file.Stem, "Drums", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sound-analyzer-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
