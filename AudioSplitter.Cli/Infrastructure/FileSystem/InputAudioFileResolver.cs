namespace AudioSplitter.Cli.Infrastructure.FileSystem;

internal static class InputAudioFileResolver
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".flac",
        ".m4a",
        ".caf",
    };

    public static IReadOnlyList<ResolvedInputAudioFile> Resolve(string inputDirectoryPath, bool recursive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] paths = Directory
            .EnumerateFiles(inputDirectoryPath, "*", searchOption)
            .Where(IsSupportedAudioFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<ResolvedInputAudioFile> resolved = new(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            string inputFilePath = paths[i];
            string parentDirectoryPath = Path.GetDirectoryName(inputFilePath) ?? inputDirectoryPath;
            string relativeDirectoryPath = Path.GetRelativePath(inputDirectoryPath, parentDirectoryPath);
            if (string.Equals(relativeDirectoryPath, ".", StringComparison.Ordinal))
            {
                relativeDirectoryPath = string.Empty;
            }

            resolved.Add(new ResolvedInputAudioFile(inputFilePath, relativeDirectoryPath));
        }

        return resolved;
    }

    private static bool IsSupportedAudioFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }
}
