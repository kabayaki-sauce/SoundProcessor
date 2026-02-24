using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Infrastructure.FileSystem;

internal static class SfftAudioFileResolver
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".flac",
        ".m4a",
        ".caf",
    };

    public static ResolvedSfftAudioFiles Resolve(string inputDirectoryPath, bool recursive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] paths = Directory
            .EnumerateFiles(inputDirectoryPath, "*", searchOption)
            .Where(IsSupportedAudioFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<SfftAudioFile> files = SelectPreferredCandidates(paths, inputDirectoryPath);
        EnsureNoDuplicateNames(files);

        int directoryCount = recursive
            ? Directory.GetDirectories(inputDirectoryPath, "*", SearchOption.AllDirectories).Length + 1
            : 1;

        return new ResolvedSfftAudioFiles(files, directoryCount);
    }

    private static List<SfftAudioFile> SelectPreferredCandidates(string[] paths, string inputDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);

        Dictionary<string, List<FileCandidate>> grouped = new(StringComparer.OrdinalIgnoreCase);
        List<string> orderedKeys = new();

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string directoryPath = Path.GetDirectoryName(path) ?? inputDirectoryPath;
            string key = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{directoryPath}\n{name}");

            if (!grouped.TryGetValue(key, out List<FileCandidate>? candidates))
            {
                candidates = new List<FileCandidate>();
                grouped[key] = candidates;
                orderedKeys.Add(key);
            }

            candidates.Add(new FileCandidate(path, name, i));
        }

        List<SfftAudioFile> resolved = new(orderedKeys.Count);
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            string key = orderedKeys[i];
            FileCandidate selected = SelectPreferred(grouped[key]);
            resolved.Add(new SfftAudioFile(selected.Name, selected.Path));
        }

        return resolved;
    }

    private static void EnsureNoDuplicateNames(List<SfftAudioFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Count; i++)
        {
            SfftAudioFile file = files[i];
            if (!names.Add(file.Name))
            {
                throw new CliException(CliErrorCode.DuplicateSfftAnalysisName, file.Name);
            }
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

    private static bool IsSupportedAudioFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }

    private sealed class FileCandidate
    {
        public FileCandidate(string path, string name, int discoveryOrder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegative(discoveryOrder);

            Path = path;
            Name = name;
            DiscoveryOrder = discoveryOrder;
        }

        public string Path { get; }

        public string Name { get; }

        public int DiscoveryOrder { get; }
    }
}
