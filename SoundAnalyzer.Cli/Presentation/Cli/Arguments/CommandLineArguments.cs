namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal sealed class CommandLineArguments
{
    public CommandLineArguments(
        long windowValue,
        AnalysisLengthUnit windowUnit,
        long hopValue,
        AnalysisLengthUnit hopUnit,
        int? targetSamplingHz,
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
        string? ffmpegPath,
        int stftProcThreads,
        int peakProcThreads,
        int stftFileThreads,
        int peakFileThreads,
        int insertQueueSize,
        bool sqliteFastMode,
        int sqliteBatchRowCount,
        bool showProgress)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stftProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stftFileThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakFileThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(insertQueueSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sqliteBatchRowCount);

        if (targetSamplingHz.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSamplingHz.Value);
        }

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        if (binCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount.Value);
        }

        WindowValue = windowValue;
        WindowUnit = windowUnit;
        HopValue = hopValue;
        HopUnit = hopUnit;
        TargetSamplingHz = targetSamplingHz;
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
        StftProcThreads = stftProcThreads;
        PeakProcThreads = peakProcThreads;
        StftFileThreads = stftFileThreads;
        PeakFileThreads = peakFileThreads;
        InsertQueueSize = insertQueueSize;
        SqliteFastMode = sqliteFastMode;
        SqliteBatchRowCount = sqliteBatchRowCount;
        ShowProgress = showProgress;
    }

    public long WindowValue { get; }

    public AnalysisLengthUnit WindowUnit { get; }

    public long HopValue { get; }

    public AnalysisLengthUnit HopUnit { get; }

    public int? TargetSamplingHz { get; }

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

    public int StftProcThreads { get; }

    public int PeakProcThreads { get; }

    public int StftFileThreads { get; }

    public int PeakFileThreads { get; }

    public int InsertQueueSize { get; }

    public bool SqliteFastMode { get; }

    public int SqliteBatchRowCount { get; }

    public bool ShowProgress { get; }

    public bool UsesSampleUnit => WindowUnit == AnalysisLengthUnit.Sample || HopUnit == AnalysisLengthUnit.Sample;

    public long WindowSizeMs => WindowUnit == AnalysisLengthUnit.Millisecond
        ? WindowValue
        : throw new InvalidOperationException("Window unit is not milliseconds.");

    public long HopMs => HopUnit == AnalysisLengthUnit.Millisecond
        ? HopValue
        : throw new InvalidOperationException("Hop unit is not milliseconds.");
}
