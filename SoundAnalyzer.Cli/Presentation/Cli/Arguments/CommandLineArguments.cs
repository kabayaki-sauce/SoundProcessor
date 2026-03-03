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
        StorageBackend storageBackend,
        string? dbFilePath,
        IReadOnlyList<string>? stems,
        string mode,
        string tableName,
        bool upsert,
        bool skipDuplicate,
        double minLimitDb,
        int? binCount,
        int? melBinCount,
        double melFminHz,
        double melFmaxHz,
        MelScaleOption melScale,
        int melPower,
        bool deleteCurrent,
        bool recursive,
        string? ffmpegPath,
        int stftProcThreads,
        int melProcThreads,
        int peakProcThreads,
        int stftFileThreads,
        int melFileThreads,
        int peakFileThreads,
        int insertQueueSize,
        bool sqliteFastMode,
        int sqliteBatchRowCount,
        bool showProgress,
        string? postgresHost,
        int? postgresPort,
        int postgresBatchRowCount,
        string? postgresDatabase,
        string? postgresUser,
        string? postgresPassword,
        string? postgresSslCertPath,
        string? postgresSslKeyPath,
        string? postgresSslRootCertPath,
        string? postgresSshHost,
        int? postgresSshPort,
        string? postgresSshUser,
        string? postgresSshPrivateKeyPath,
        string? postgresSshKnownHostsPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stftProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stftFileThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melFileThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakFileThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(insertQueueSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sqliteBatchRowCount);

        if (storageBackend == StorageBackend.Sqlite)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dbFilePath);
        }

        if (targetSamplingHz.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSamplingHz.Value);
        }

        if (postgresPort.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postgresPort.Value);
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postgresBatchRowCount);

        if (postgresSshPort.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postgresSshPort.Value);
        }

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        if (binCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount.Value);
        }

        if (melBinCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melBinCount.Value);
        }

        if (double.IsNaN(melFminHz) || double.IsInfinity(melFminHz) || melFminHz < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(melFminHz));
        }

        if (double.IsNaN(melFmaxHz) || double.IsInfinity(melFmaxHz) || melFmaxHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(melFmaxHz));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(melFmaxHz, melFminHz);

        if (melPower is not 1 and not 2)
        {
            throw new ArgumentOutOfRangeException(nameof(melPower));
        }

        WindowValue = windowValue;
        WindowUnit = windowUnit;
        HopValue = hopValue;
        HopUnit = hopUnit;
        TargetSamplingHz = targetSamplingHz;
        InputDirectoryPath = inputDirectoryPath;
        StorageBackend = storageBackend;
        DbFilePath = string.IsNullOrWhiteSpace(dbFilePath) ? null : dbFilePath.Trim();
        Stems = stems;
        Mode = mode;
        TableName = tableName;
        Upsert = upsert;
        SkipDuplicate = skipDuplicate;
        MinLimitDb = minLimitDb;
        BinCount = binCount;
        MelBinCount = melBinCount;
        MelFminHz = melFminHz;
        MelFmaxHz = melFmaxHz;
        MelScale = melScale;
        MelPower = melPower;
        DeleteCurrent = deleteCurrent;
        Recursive = recursive;
        FfmpegPath = ffmpegPath;
        StftProcThreads = stftProcThreads;
        MelProcThreads = melProcThreads;
        PeakProcThreads = peakProcThreads;
        StftFileThreads = stftFileThreads;
        MelFileThreads = melFileThreads;
        PeakFileThreads = peakFileThreads;
        InsertQueueSize = insertQueueSize;
        SqliteFastMode = sqliteFastMode;
        SqliteBatchRowCount = sqliteBatchRowCount;
        ShowProgress = showProgress;
        PostgresHost = string.IsNullOrWhiteSpace(postgresHost) ? null : postgresHost.Trim();
        PostgresPort = postgresPort;
        PostgresBatchRowCount = postgresBatchRowCount;
        PostgresDatabase = string.IsNullOrWhiteSpace(postgresDatabase) ? null : postgresDatabase.Trim();
        PostgresUser = string.IsNullOrWhiteSpace(postgresUser) ? null : postgresUser.Trim();
        PostgresPassword = string.IsNullOrWhiteSpace(postgresPassword) ? null : postgresPassword;
        PostgresSslCertPath = string.IsNullOrWhiteSpace(postgresSslCertPath) ? null : postgresSslCertPath.Trim();
        PostgresSslKeyPath = string.IsNullOrWhiteSpace(postgresSslKeyPath) ? null : postgresSslKeyPath.Trim();
        PostgresSslRootCertPath = string.IsNullOrWhiteSpace(postgresSslRootCertPath) ? null : postgresSslRootCertPath.Trim();
        PostgresSshHost = string.IsNullOrWhiteSpace(postgresSshHost) ? null : postgresSshHost.Trim();
        PostgresSshPort = postgresSshPort;
        PostgresSshUser = string.IsNullOrWhiteSpace(postgresSshUser) ? null : postgresSshUser.Trim();
        PostgresSshPrivateKeyPath = string.IsNullOrWhiteSpace(postgresSshPrivateKeyPath) ? null : postgresSshPrivateKeyPath.Trim();
        PostgresSshKnownHostsPath = string.IsNullOrWhiteSpace(postgresSshKnownHostsPath) ? null : postgresSshKnownHostsPath.Trim();
    }

    public long WindowValue { get; }

    public AnalysisLengthUnit WindowUnit { get; }

    public long HopValue { get; }

    public AnalysisLengthUnit HopUnit { get; }

    public int? TargetSamplingHz { get; }

    public string InputDirectoryPath { get; }

    public StorageBackend StorageBackend { get; }

    public string? DbFilePath { get; }

    public IReadOnlyList<string>? Stems { get; }

    public string Mode { get; }

    public string TableName { get; }

    public bool Upsert { get; }

    public bool SkipDuplicate { get; }

    public double MinLimitDb { get; }

    public int? BinCount { get; }

    public int? MelBinCount { get; }

    public double MelFminHz { get; }

    public double MelFmaxHz { get; }

    public MelScaleOption MelScale { get; }

    public int MelPower { get; }

    public bool DeleteCurrent { get; }

    public bool Recursive { get; }

    public string? FfmpegPath { get; }

    public int StftProcThreads { get; }

    public int MelProcThreads { get; }

    public int PeakProcThreads { get; }

    public int StftFileThreads { get; }

    public int MelFileThreads { get; }

    public int PeakFileThreads { get; }

    public int InsertQueueSize { get; }

    public bool SqliteFastMode { get; }

    public int SqliteBatchRowCount { get; }

    public bool ShowProgress { get; }

    public string? PostgresHost { get; }

    public int? PostgresPort { get; }

    public int PostgresBatchRowCount { get; }

    public string? PostgresDatabase { get; }

    public string? PostgresUser { get; }

    public string? PostgresPassword { get; }

    public string? PostgresSslCertPath { get; }

    public string? PostgresSslKeyPath { get; }

    public string? PostgresSslRootCertPath { get; }

    public string? PostgresSshHost { get; }

    public int? PostgresSshPort { get; }

    public string? PostgresSshUser { get; }

    public string? PostgresSshPrivateKeyPath { get; }

    public string? PostgresSshKnownHostsPath { get; }

    public bool UsesSampleUnit => WindowUnit == AnalysisLengthUnit.Sample || HopUnit == AnalysisLengthUnit.Sample;

    public bool IsPostgresMode => StorageBackend == StorageBackend.Postgres;

    public long WindowSizeMs => WindowUnit == AnalysisLengthUnit.Millisecond
        ? WindowValue
        : throw new InvalidOperationException("Window unit is not milliseconds.");

    public long HopMs => HopUnit == AnalysisLengthUnit.Millisecond
        ? HopValue
        : throw new InvalidOperationException("Hop unit is not milliseconds.");
}
