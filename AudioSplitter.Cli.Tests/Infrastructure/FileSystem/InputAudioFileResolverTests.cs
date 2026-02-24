using AudioSplitter.Cli.Infrastructure.FileSystem;

namespace AudioSplitter.Cli.Tests.Infrastructure.FileSystem;

public sealed class InputAudioFileResolverTests
{
    [Fact]
    public void Resolve_ShouldScanTopDirectoryOnly_WhenRecursiveIsFalse()
    {
        string root = CreateTempDirectory();
        try
        {
            string topLevel = Path.Combine(root, "Top.wav");
            File.WriteAllText(topLevel, "top");

            string subDir = Path.Combine(root, "Sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "Nested.wav"), "nested");

            IReadOnlyList<ResolvedInputAudioFile> resolved = InputAudioFileResolver.Resolve(root, recursive: false);

            Assert.Single(resolved);
            Assert.Equal(topLevel, resolved[0].InputFilePath, ignoreCase: true);
            Assert.Equal(string.Empty, resolved[0].RelativeDirectoryPath, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldIncludeNestedFiles_WhenRecursiveIsTrue()
    {
        string root = CreateTempDirectory();
        try
        {
            string topLevel = Path.Combine(root, "Top.flac");
            File.WriteAllText(topLevel, "top");

            string subDir = Path.Combine(root, "Sub");
            Directory.CreateDirectory(subDir);
            string nested = Path.Combine(subDir, "Nested.wav");
            File.WriteAllText(nested, "nested");

            IReadOnlyList<ResolvedInputAudioFile> resolved = InputAudioFileResolver.Resolve(root, recursive: true);

            Assert.Equal(2, resolved.Count);
            Assert.Contains(
                resolved,
                item => string.Equals(item.InputFilePath, topLevel, StringComparison.OrdinalIgnoreCase)
                    && item.RelativeDirectoryPath.Length == 0);
            Assert.Contains(
                resolved,
                item => string.Equals(item.InputFilePath, nested, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.RelativeDirectoryPath, "Sub", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldFilterUnsupportedExtensions()
    {
        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "A.wav"), "wav");
            File.WriteAllText(Path.Combine(root, "B.CAF"), "caf");
            File.WriteAllText(Path.Combine(root, "C.txt"), "txt");

            IReadOnlyList<ResolvedInputAudioFile> resolved = InputAudioFileResolver.Resolve(root, recursive: true);

            Assert.Equal(2, resolved.Count);
            Assert.DoesNotContain(
                resolved,
                item => string.Equals(Path.GetExtension(item.InputFilePath), ".txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ShouldSortByPathCaseInsensitive()
    {
        string root = CreateTempDirectory();
        try
        {
            string lower = Path.Combine(root, "a.wav");
            string upper = Path.Combine(root, "B.wav");
            File.WriteAllText(upper, "b");
            File.WriteAllText(lower, "a");

            IReadOnlyList<ResolvedInputAudioFile> resolved = InputAudioFileResolver.Resolve(root, recursive: false);

            Assert.Equal(2, resolved.Count);
            Assert.Equal(lower, resolved[0].InputFilePath, ignoreCase: true);
            Assert.Equal(upper, resolved[1].InputFilePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"audio-splitter-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
