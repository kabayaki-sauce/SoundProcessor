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
        bool showProgress,
        string? postgresHost,
        int? postgresPort,
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakProcThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stftFileThreads);
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
        PostgresHost = string.IsNullOrWhiteSpace(postgresHost) ? null : postgresHost.Trim();
        PostgresPort = postgresPort;
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

    public string? PostgresHost { get; }

    public int? PostgresPort { get; }

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
