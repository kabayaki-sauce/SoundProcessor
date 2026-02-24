namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal sealed class CommandLineArguments
{
    public CommandLineArguments(
        long windowSizeMs,
        long hopMs,
        string inputDirectoryPath,
        string dbFilePath,
        IReadOnlyList<string>? stems,
        string mode,
        string tableName,
        bool upsert,
        bool skipDuplicate,
        double minLimitDb,
        int? binCount,
        bool deleteCurrent,
        bool recursive,
        string? ffmpegPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSizeMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        if (binCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount.Value);
        }

        WindowSizeMs = windowSizeMs;
        HopMs = hopMs;
        InputDirectoryPath = inputDirectoryPath;
        DbFilePath = dbFilePath;
        Stems = stems;
        Mode = mode;
        TableName = tableName;
        Upsert = upsert;
        SkipDuplicate = skipDuplicate;
        MinLimitDb = minLimitDb;
        BinCount = binCount;
        DeleteCurrent = deleteCurrent;
        Recursive = recursive;
        FfmpegPath = ffmpegPath;
    }

    public long WindowSizeMs { get; }

    public long HopMs { get; }

    public string InputDirectoryPath { get; }

    public string DbFilePath { get; }

    public IReadOnlyList<string>? Stems { get; }

    public string Mode { get; }

    public string TableName { get; }

    public bool Upsert { get; }

    public bool SkipDuplicate { get; }

    public double MinLimitDb { get; }

    public int? BinCount { get; }

    public bool DeleteCurrent { get; }

    public bool Recursive { get; }

    public string? FfmpegPath { get; }
}
