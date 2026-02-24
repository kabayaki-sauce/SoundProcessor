using System.Globalization;
using System.Text.RegularExpressions;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal static partial class CommandLineParser
{
    private const int DefaultProcessingThreads = 1;
    private const int DefaultFileThreads = 1;
    private const int DefaultInsertQueueSize = 1024;
    private const int DefaultSqliteBatchRowCount = 512;
    private const int DefaultPostgresBatchRowCount = 1;
    private const int DefaultPostgresSshPort = 22;

    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return CommandLineParseResult.Help();
        }

        string? windowSizeText = null;
        string? hopText = null;
        string? targetSamplingText = null;
        string? inputDirPath = null;
        string? dbFilePath = null;
        string? stemsText = null;
        string? modeText = null;
        string? tableNameOverride = null;
        string? minLimitDbText = null;
        string? binCountText = null;
        string? ffmpegPath = null;
        string? stftProcThreadsText = null;
        string? peakProcThreadsText = null;
        string? stftFileThreadsText = null;
        string? peakFileThreadsText = null;
        string? insertQueueSizeText = null;
        string? sqliteBatchRowCountText = null;
        string? postgresHostText = null;
        string? postgresPortText = null;
        string? postgresBatchRowCountText = null;
        string? postgresDbText = null;
        string? postgresUserText = null;
        string? postgresPasswordText = null;
        string? postgresSslCertPathText = null;
        string? postgresSslKeyPathText = null;
        string? postgresSslRootCertPathText = null;
        string? postgresSshHostText = null;
        string? postgresSshPortText = null;
        string? postgresSshUserText = null;
        string? postgresSshPrivateKeyPathText = null;
        string? postgresSshKnownHostsPathText = null;
        bool upsert = false;
        bool skipDuplicate = false;
        bool deleteCurrent = false;
        bool recursive = false;
        bool sqliteFastMode = false;
        bool showProgress = false;
        bool postgres = false;

        List<string> errors = new();
        List<string> warnings = new();

        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (IsHelp(token))
            {
                return CommandLineParseResult.Help();
            }

            if (MatchesOption(token, ConsoleTexts.UpsertOption))
            {
                upsert = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.SkipDuplicateOption))
            {
                skipDuplicate = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.DeleteCurrentOption))
            {
                deleteCurrent = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.RecursiveOption))
            {
                recursive = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ShowProgressOption))
            {
                showProgress = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.SqliteFastModeOption))
            {
                sqliteFastMode = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresOption))
            {
                postgres = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.WindowSizeOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    windowSizeText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.HopOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    hopText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.TargetSamplingOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    targetSamplingText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.InputDirOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    inputDirPath = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.DbFileOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    dbFilePath = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.StemsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    stemsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ModeOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    modeText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.TableNameOverrideOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    tableNameOverride = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.MinLimitDbOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    minLimitDbText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.BinCountOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    binCountText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.FfmpegPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    ffmpegPath = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.StftProcThreadsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    stftProcThreadsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PeakProcThreadsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    peakProcThreadsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.StftFileThreadsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    stftFileThreadsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PeakFileThreadsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    peakFileThreadsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.InsertQueueSizeOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    insertQueueSizeText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.SqliteBatchRowCountOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    sqliteBatchRowCountText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresHostOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresHostText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresPortOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresPortText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresDbOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresDbText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresBatchRowCountOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresBatchRowCountText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresUserOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresUserText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresPasswordOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresPasswordText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSslCertPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSslCertPathText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSslKeyPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSslKeyPathText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSslRootCertPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSslRootCertPathText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSshHostOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSshHostText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSshPortOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSshPortText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSshUserOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSshUserText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSshPrivateKeyPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSshPrivateKeyPathText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.PostgresSshKnownHostsPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    postgresSshKnownHostsPathText = value;
                }

                continue;
            }

            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.UnknownOptionPrefix, token));
        }

        if (windowSizeText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.WindowSizeOption));
        }

        if (hopText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.HopOption));
        }

        if (inputDirPath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.InputDirOption));
        }

        if (modeText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.ModeOption));
        }

        if (!postgres && dbFilePath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.DbFileOption));
        }

        if (postgres && dbFilePath is not null)
        {
            errors.Add(ConsoleTexts.PostgresDbFileConflictText);
        }

        bool hasPostgresHost = IsSpecified(postgresHostText);
        bool hasPostgresPort = IsSpecified(postgresPortText);
        bool hasPostgresBatchRowCount = IsSpecified(postgresBatchRowCountText);
        bool hasPostgresDb = IsSpecified(postgresDbText);
        bool hasPostgresUser = IsSpecified(postgresUserText);
        bool hasPostgresPassword = IsSpecified(postgresPasswordText);
        bool hasPostgresSslCertPath = IsSpecified(postgresSslCertPathText);
        bool hasPostgresSslKeyPath = IsSpecified(postgresSslKeyPathText);
        bool hasPostgresSslRootPath = IsSpecified(postgresSslRootCertPathText);
        bool hasPostgresSshHost = IsSpecified(postgresSshHostText);
        bool hasPostgresSshPort = IsSpecified(postgresSshPortText);
        bool hasPostgresSshUser = IsSpecified(postgresSshUserText);
        bool hasPostgresSshPrivateKeyPath = IsSpecified(postgresSshPrivateKeyPathText);
        bool hasPostgresSshKnownHostsPath = IsSpecified(postgresSshKnownHostsPathText);

        string[] postgresOnlyOptions =
        [
            hasPostgresHost ? ConsoleTexts.PostgresHostOption : string.Empty,
            hasPostgresPort ? ConsoleTexts.PostgresPortOption : string.Empty,
            hasPostgresBatchRowCount ? ConsoleTexts.PostgresBatchRowCountOption : string.Empty,
            hasPostgresDb ? ConsoleTexts.PostgresDbOption : string.Empty,
            hasPostgresUser ? ConsoleTexts.PostgresUserOption : string.Empty,
            hasPostgresPassword ? ConsoleTexts.PostgresPasswordOption : string.Empty,
            hasPostgresSslCertPath ? ConsoleTexts.PostgresSslCertPathOption : string.Empty,
            hasPostgresSslKeyPath ? ConsoleTexts.PostgresSslKeyPathOption : string.Empty,
            hasPostgresSslRootPath ? ConsoleTexts.PostgresSslRootCertPathOption : string.Empty,
            hasPostgresSshHost ? ConsoleTexts.PostgresSshHostOption : string.Empty,
            hasPostgresSshPort ? ConsoleTexts.PostgresSshPortOption : string.Empty,
            hasPostgresSshUser ? ConsoleTexts.PostgresSshUserOption : string.Empty,
            hasPostgresSshPrivateKeyPath ? ConsoleTexts.PostgresSshPrivateKeyPathOption : string.Empty,
            hasPostgresSshKnownHostsPath ? ConsoleTexts.PostgresSshKnownHostsPathOption : string.Empty,
        ];

        if (!postgres)
        {
            for (int i = 0; i < postgresOnlyOptions.Length; i++)
            {
                if (postgresOnlyOptions[i].Length == 0)
                {
                    continue;
                }

                errors.Add(ConsoleTexts.WithValue(
                    ConsoleTexts.PostgresOptionRequiresPostgresModePrefix,
                    postgresOnlyOptions[i]));
            }
        }
        else
        {
            if (sqliteFastMode)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.SqliteOptionNotAllowedWithPostgresPrefix, ConsoleTexts.SqliteFastModeOption));
            }

            if (sqliteBatchRowCountText is not null)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.SqliteOptionNotAllowedWithPostgresPrefix, ConsoleTexts.SqliteBatchRowCountOption));
            }

            if (!hasPostgresHost)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresHostOption));
            }

            if (!hasPostgresPort)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresPortOption));
            }

            if (!hasPostgresDb)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresDbOption));
            }

            if (!hasPostgresUser)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresUserOption));
            }

            if (hasPostgresSslCertPath != hasPostgresSslKeyPath)
            {
                errors.Add(ConsoleTexts.PostgresSslPairRequiredText);
            }

            bool hasPostgresSslPair = hasPostgresSslCertPath && hasPostgresSslKeyPath;
            if (hasPostgresPassword && hasPostgresSslPair)
            {
                errors.Add(ConsoleTexts.PostgresAuthConflictText);
            }

            if (!hasPostgresPassword && !hasPostgresSslPair)
            {
                warnings.Add(ConsoleTexts.PostgresAuthNotProvidedWarningText);
            }

            if (hasPostgresSshHost)
            {
                if (!hasPostgresSshUser)
                {
                    errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresSshUserOption));
                }

                if (!hasPostgresSshPrivateKeyPath)
                {
                    errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresSshPrivateKeyPathOption));
                }

                if (!hasPostgresSshKnownHostsPath)
                {
                    errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.PostgresSshKnownHostsPathOption));
                }
            }
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        string modeValue = modeText!;
        bool isPeakMode = string.Equals(modeValue, ConsoleTexts.PeakAnalysisMode, StringComparison.OrdinalIgnoreCase);
        bool isStftMode = string.Equals(modeValue, ConsoleTexts.StftAnalysisMode, StringComparison.OrdinalIgnoreCase);
        if (!isPeakMode && !isStftMode)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidModePrefix, modeValue));
        }

        if (!TryParseLength(windowSizeText!, out AnalysisLengthArgument windowLength))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, windowSizeText!));
        }

        if (!TryParseLength(hopText!, out AnalysisLengthArgument hopLength))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, hopText!));
        }

        int? targetSamplingHz = null;
        if (!string.IsNullOrWhiteSpace(targetSamplingText))
        {
            if (!TryParseSamplingHz(targetSamplingText!, out int parsedSamplingHz))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidSamplingPrefix, targetSamplingText!));
            }
            else
            {
                targetSamplingHz = parsedSamplingHz;
            }
        }

        IReadOnlyList<string>? stems = null;
        if (!string.IsNullOrWhiteSpace(stemsText))
        {
            if (!TryParseStems(stemsText!, out List<string> parsedStems))
            {
                errors.Add(ConsoleTexts.InvalidStemsText);
            }
            else
            {
                stems = parsedStems;
            }
        }

        int? binCount = null;
        if (!string.IsNullOrWhiteSpace(binCountText))
        {
            if (!TryParsePositiveInt(binCountText!, out int parsedBinCount))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, binCountText!));
            }
            else
            {
                binCount = parsedBinCount;
            }
        }

        int stftProcThreads = DefaultProcessingThreads;
        if (!string.IsNullOrWhiteSpace(stftProcThreadsText))
        {
            if (!TryParsePositiveInt(stftProcThreadsText!, out int parsedStftProcThreads))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, stftProcThreadsText!));
            }
            else
            {
                stftProcThreads = parsedStftProcThreads;
            }
        }

        int peakProcThreads = DefaultProcessingThreads;
        if (!string.IsNullOrWhiteSpace(peakProcThreadsText))
        {
            if (!TryParsePositiveInt(peakProcThreadsText!, out int parsedPeakProcThreads))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, peakProcThreadsText!));
            }
            else
            {
                peakProcThreads = parsedPeakProcThreads;
            }
        }

        int stftFileThreads = DefaultFileThreads;
        if (!string.IsNullOrWhiteSpace(stftFileThreadsText))
        {
            if (!TryParsePositiveInt(stftFileThreadsText!, out int parsedStftFileThreads))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, stftFileThreadsText!));
            }
            else
            {
                stftFileThreads = parsedStftFileThreads;
            }
        }

        int peakFileThreads = DefaultFileThreads;
        if (!string.IsNullOrWhiteSpace(peakFileThreadsText))
        {
            if (!TryParsePositiveInt(peakFileThreadsText!, out int parsedPeakFileThreads))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, peakFileThreadsText!));
            }
            else
            {
                peakFileThreads = parsedPeakFileThreads;
            }
        }

        int insertQueueSize = DefaultInsertQueueSize;
        if (!string.IsNullOrWhiteSpace(insertQueueSizeText))
        {
            if (!TryParsePositiveInt(insertQueueSizeText!, out int parsedInsertQueueSize))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, insertQueueSizeText!));
            }
            else
            {
                insertQueueSize = parsedInsertQueueSize;
            }
        }

        int sqliteBatchRowCount = DefaultSqliteBatchRowCount;
        if (!string.IsNullOrWhiteSpace(sqliteBatchRowCountText))
        {
            if (!TryParsePositiveInt(sqliteBatchRowCountText!, out int parsedSqliteBatchRowCount))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, sqliteBatchRowCountText!));
            }
            else
            {
                sqliteBatchRowCount = parsedSqliteBatchRowCount;
            }
        }

        int? postgresPort = null;
        if (!string.IsNullOrWhiteSpace(postgresPortText))
        {
            if (!TryParsePositiveInt(postgresPortText!, out int parsedPostgresPort))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, postgresPortText!));
            }
            else
            {
                postgresPort = parsedPostgresPort;
            }
        }

        int postgresBatchRowCount = DefaultPostgresBatchRowCount;
        if (!string.IsNullOrWhiteSpace(postgresBatchRowCountText))
        {
            if (!TryParsePositiveInt(postgresBatchRowCountText!, out int parsedPostgresBatchRowCount))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, postgresBatchRowCountText!));
            }
            else
            {
                postgresBatchRowCount = parsedPostgresBatchRowCount;
            }
        }

        int? postgresSshPort = null;
        if (!string.IsNullOrWhiteSpace(postgresSshPortText))
        {
            if (!TryParsePositiveInt(postgresSshPortText!, out int parsedPostgresSshPort))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, postgresSshPortText!));
            }
            else
            {
                postgresSshPort = parsedPostgresSshPort;
            }
        }

        bool usesSampleUnit = windowLength.IsSample || hopLength.IsSample;

        if (isStftMode)
        {
            if (binCount is null)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.BinCountOption));
            }

            if (stems is not null)
            {
                AddModeWarning(warnings, ConsoleTexts.StemsOption);
                stems = null;
            }

            if (usesSampleUnit && !targetSamplingHz.HasValue)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.TargetSamplingOption));
            }

            if (!string.IsNullOrWhiteSpace(peakProcThreadsText))
            {
                AddModeWarning(warnings, ConsoleTexts.PeakProcThreadsOption);
            }

            if (!string.IsNullOrWhiteSpace(peakFileThreadsText))
            {
                AddModeWarning(warnings, ConsoleTexts.PeakFileThreadsOption);
            }
        }

        if (isPeakMode)
        {
            if (!string.IsNullOrWhiteSpace(binCountText))
            {
                AddModeWarning(warnings, ConsoleTexts.BinCountOption);
                binCount = null;
            }

            if (deleteCurrent)
            {
                AddModeWarning(warnings, ConsoleTexts.DeleteCurrentOption);
                deleteCurrent = false;
            }

            if (recursive)
            {
                AddModeWarning(warnings, ConsoleTexts.RecursiveOption);
                recursive = false;
            }

            if (usesSampleUnit)
            {
                errors.Add(ConsoleTexts.SampleUnitOnlyForStftText);
            }

            if (targetSamplingText is not null)
            {
                AddModeWarning(warnings, ConsoleTexts.TargetSamplingOption);
                targetSamplingHz = null;
            }

            if (!string.IsNullOrWhiteSpace(stftProcThreadsText))
            {
                AddModeWarning(warnings, ConsoleTexts.StftProcThreadsOption);
            }

            if (!string.IsNullOrWhiteSpace(stftFileThreadsText))
            {
                AddModeWarning(warnings, ConsoleTexts.StftFileThreadsOption);
            }
        }

        string defaultTableName = isStftMode ? ConsoleTexts.DefaultStftTableName : ConsoleTexts.DefaultPeakTableName;
        string tableName = string.IsNullOrWhiteSpace(tableNameOverride)
            ? defaultTableName
            : tableNameOverride!.Trim();

        if (!IsValidTableName(tableName))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTableNamePrefix, tableName));
        }

        double minLimitDb = -120.0;
        if (!string.IsNullOrWhiteSpace(minLimitDbText))
        {
            if (!double.TryParse(minLimitDbText, NumberStyles.Float, CultureInfo.InvariantCulture, out minLimitDb)
                || double.IsNaN(minLimitDb)
                || double.IsInfinity(minLimitDb))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidNumberPrefix, minLimitDbText!));
            }
        }

        if (upsert && skipDuplicate)
        {
            errors.Add(ConsoleTexts.UpsertSkipConflictText);
        }

        if (postgres && hasPostgresSshHost && !postgresSshPort.HasValue)
        {
            postgresSshPort = DefaultPostgresSshPort;
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        StorageBackend backend = postgres ? StorageBackend.Postgres : StorageBackend.Sqlite;
        string mode = isStftMode ? ConsoleTexts.StftAnalysisMode : ConsoleTexts.PeakAnalysisMode;
        CommandLineArguments arguments = new(
            windowLength.Value,
            windowLength.Unit,
            hopLength.Value,
            hopLength.Unit,
            targetSamplingHz,
            inputDirPath!,
            backend,
            dbFilePath,
            stems,
            mode,
            tableName,
            upsert,
            skipDuplicate,
            minLimitDb,
            binCount,
            deleteCurrent,
            recursive,
            ffmpegPath,
            stftProcThreads,
            peakProcThreads,
            stftFileThreads,
            peakFileThreads,
            insertQueueSize,
            sqliteFastMode,
            sqliteBatchRowCount,
            showProgress,
            postgresHostText,
            postgresPort,
            postgresBatchRowCount,
            postgresDbText,
            postgresUserText,
            postgresPasswordText,
            postgresSslCertPathText,
            postgresSslKeyPathText,
            postgresSslRootCertPathText,
            postgresSshHostText,
            postgresSshPort,
            postgresSshUserText,
            postgresSshPrivateKeyPathText,
            postgresSshKnownHostsPathText);
        return CommandLineParseResult.Success(arguments, warnings);
    }

    internal static bool IsValidTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        return TableNamePattern().IsMatch(tableName.Trim());
    }

    internal static bool TryParsePositiveInt(string text, out int value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return false;
        }

        if (parsed <= 0 || parsed > int.MaxValue)
        {
            return false;
        }

        value = checked((int)parsed);
        return true;
    }

    internal static bool TryParseStems(string text, out List<string> stems)
    {
        ArgumentNullException.ThrowIfNull(text);

        stems = text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return stems.Count > 0;
    }

    private static bool TryParseLength(string text, out AnalysisLengthArgument length)
    {
        length = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = LengthPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double scalar)
            || double.IsNaN(scalar)
            || double.IsInfinity(scalar))
        {
            return false;
        }

        string unit = match.Groups["unit"].Value;
        if (unit.Equals("sample", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("samples", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseIntegralPositiveLong(scalar, out long sampleCount))
            {
                return false;
            }

            length = new AnalysisLengthArgument(sampleCount, AnalysisLengthUnit.Sample);
            return true;
        }

        double totalMilliseconds = unit.ToLowerInvariant() switch
        {
            "ms" => scalar,
            "s" => scalar * 1000.0,
            "m" => scalar * 60_000.0,
            _ => double.NaN,
        };

        if (double.IsNaN(totalMilliseconds) || double.IsInfinity(totalMilliseconds))
        {
            return false;
        }

        if (!TryParseIntegralPositiveLong(totalMilliseconds, out long milliseconds))
        {
            return false;
        }

        length = new AnalysisLengthArgument(milliseconds, AnalysisLengthUnit.Millisecond);
        return true;
    }

    private static bool TryParseSamplingHz(string text, out int samplingHz)
    {
        samplingHz = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = SamplingPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out samplingHz)
            && samplingHz > 0;
    }

    private static bool TryParseIntegralPositiveLong(double value, out long integral)
    {
        integral = default;

        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return false;
        }

        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (Math.Abs(value - rounded) > 1e-9 || rounded > long.MaxValue)
        {
            return false;
        }

        integral = checked((long)rounded);
        return true;
    }

    private static bool MatchesOption(string token, string optionName)
    {
        return string.Equals(token, optionName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecified(string? optionText)
    {
        return !string.IsNullOrWhiteSpace(optionText);
    }

    private static bool IsHelp(string token)
    {
        return string.Equals(token, ConsoleTexts.HelpOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, ConsoleTexts.ShortHelpOption, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        List<string> errors,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(optionName);
        ArgumentNullException.ThrowIfNull(errors);

        value = string.Empty;

        int valueIndex = index + 1;
        if (valueIndex >= args.Count)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingValuePrefix, optionName));
            return false;
        }

        value = args[valueIndex];
        index = valueIndex;
        return true;
    }

    private static void AddModeWarning(List<string> warnings, string optionName)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionName);
        warnings.Add(ConsoleTexts.WithValue(ConsoleTexts.IncompatibleOptionIgnoredPrefix, optionName));
    }

    [GeneratedRegex(
        @"^(?<value>[+-]?\d+(?:\.\d+)?)(?<unit>ms|s|m|sample|samples)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LengthPattern();

    [GeneratedRegex(
        @"^(?<value>[1-9]\d*)hz$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SamplingPattern();

    [GeneratedRegex(
        @"^[A-Za-z_][A-Za-z0-9_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex TableNamePattern();
}
