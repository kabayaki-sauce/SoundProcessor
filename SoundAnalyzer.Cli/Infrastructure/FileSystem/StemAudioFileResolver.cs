namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal static class StemAudioFileResolver
{
    public static ResolvedStemAudioFiles Resolve(string inputDirectoryPath, IReadOnlyList<string>? stems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);

        string[] subDirectories = Directory
            .GetDirectories(inputDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<StemAudioFile> resolvedFiles = new();
        int skippedStemCount = 0;

        for (int directoryIndex = 0; directoryIndex < subDirectories.Length; directoryIndex++)
        {
            string subDirectoryPath = subDirectories[directoryIndex];
            string name = Path.GetFileName(subDirectoryPath);

            IReadOnlyList<FileCandidate> candidates = Directory
                .EnumerateFiles(subDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                .Select((path, index) => new FileCandidate(path, index))
                .ToArray();

            ResolveForDirectory(name, candidates, stems, resolvedFiles, ref skippedStemCount);
        }

        return new ResolvedStemAudioFiles(resolvedFiles, subDirectories.Length, skippedStemCount);
    }

    private static void ResolveForDirectory(
        string name,
        IReadOnlyList<FileCandidate> candidates,
        IReadOnlyList<string>? stems,
        List<StemAudioFile> resolvedFiles,
        ref int skippedStemCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(resolvedFiles);

        Dictionary<string, List<FileCandidate>> grouped = new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedStemKeys = new();

        for (int i = 0; i < candidates.Count; i++)
        {
            FileCandidate candidate = candidates[i];
            string stem = Path.GetFileNameWithoutExtension(candidate.Path);
            if (string.IsNullOrWhiteSpace(stem))
            {
                continue;
            }

            if (!grouped.TryGetValue(stem, out List<FileCandidate>? list))
            {
                list = new List<FileCandidate>();
                grouped[stem] = list;
                orderedStemKeys.Add(stem);
            }

            list.Add(candidate);
        }

        if (stems is null)
        {
            for (int i = 0; i < orderedStemKeys.Count; i++)
            {
                string key = orderedStemKeys[i];
                List<FileCandidate> options = grouped[key];
                FileCandidate selected = SelectPreferred(options);
                string stem = Path.GetFileNameWithoutExtension(selected.Path);
                resolvedFiles.Add(new StemAudioFile(name, stem, selected.Path));
            }

            return;
        }

        for (int i = 0; i < stems.Count; i++)
        {
            string requestedStem = stems[i];
            if (!grouped.TryGetValue(requestedStem, out List<FileCandidate>? options) || options.Count == 0)
            {
                skippedStemCount++;
                continue;
            }

            FileCandidate selected = SelectPreferred(options);
            string stem = Path.GetFileNameWithoutExtension(selected.Path);
            resolvedFiles.Add(new StemAudioFile(name, stem, selected.Path));
        }
    }

    private static FileCandidate SelectPreferred(IReadOnlyList<FileCandidate> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            throw new ArgumentException("At least one file candidate is required.", nameof(options));
        }

        FileCandidate? best = null;
        int bestPriority = int.MaxValue;
        int bestOrder = int.MaxValue;

        for (int i = 0; i < options.Count; i++)
        {
            FileCandidate candidate = options[i];
            int priority = GetExtensionPriority(candidate.Path);
            if (best is null || priority < bestPriority || (priority == bestPriority && candidate.DiscoveryOrder < bestOrder))
            {
                best = candidate;
                bestPriority = priority;
                bestOrder = candidate.DiscoveryOrder;
            }
        }

        return best!;
    }

    private static int GetExtensionPriority(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "wav" => 0,
            "flac" => 1,
            "m4a" => 2,
            "caf" => 2,
            _ => 3,
        };
    }

    private sealed class FileCandidate
    {
        public FileCandidate(string path, int discoveryOrder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentOutOfRangeException.ThrowIfNegative(discoveryOrder);

            Path = path;
            DiscoveryOrder = discoveryOrder;
        }

        public string Path { get; }

        public int DiscoveryOrder { get; }
    }
}
