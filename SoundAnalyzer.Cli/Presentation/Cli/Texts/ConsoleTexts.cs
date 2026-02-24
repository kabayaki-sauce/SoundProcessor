using System.Globalization;

namespace SoundAnalyzer.Cli.Presentation.Cli.Texts;

internal static class ConsoleTexts
{
    public const string WindowSizeOption = "--window-size";
    public const string HopOption = "--hop";
    public const string TargetSamplingOption = "--target-sampling";
    public const string InputDirOption = "--input-dir";
    public const string DbFileOption = "--db-file";
    public const string StemsOption = "--stems";
    public const string ModeOption = "--mode";
    public const string TableNameOverrideOption = "--table-name-override";
    public const string UpsertOption = "--upsert";
    public const string SkipDuplicateOption = "--skip-duplicate";
    public const string MinLimitDbOption = "--min-limit-db";
    public const string BinCountOption = "--bin-count";
    public const string DeleteCurrentOption = "--delete-current";
    public const string RecursiveOption = "--recursive";
    public const string StftProcThreadsOption = "--stft-proc-threads";
    public const string PeakProcThreadsOption = "--peak-proc-threads";
    public const string StftFileThreadsOption = "--stft-file-threads";
    public const string PeakFileThreadsOption = "--peak-file-threads";
    public const string InsertQueueSizeOption = "--insert-queue-size";
    public const string SqliteFastModeOption = "--sqlite-fast-mode";
    public const string SqliteBatchRowCountOption = "--sqlite-batch-row-count";
    public const string FfmpegPathOption = "--ffmpeg-path";
    public const string ShowProgressOption = "--show-progress";
    public const string PostgresOption = "--postgres";
    public const string PostgresHostOption = "--postgres-host";
    public const string PostgresPortOption = "--postgres-port";
    public const string PostgresDbOption = "--postgres-db";
    public const string PostgresUserOption = "--postgres-user";
    public const string PostgresPasswordOption = "--postgres-password";
    public const string PostgresSslCertPathOption = "--postgres-sslcert-path";
    public const string PostgresSslKeyPathOption = "--postgres-sslkey-path";
    public const string PostgresSslRootCertPathOption = "--postgres-sslrootcert-path";
    public const string PostgresSshHostOption = "--postgres-ssh-host";
    public const string PostgresSshPortOption = "--postgres-ssh-port";
    public const string PostgresSshUserOption = "--postgres-ssh-user";
    public const string PostgresSshPrivateKeyPathOption = "--postgres-ssh-private-key-path";
    public const string PostgresSshKnownHostsPathOption = "--postgres-ssh-known-hosts-path";
    public const string PostgresBatchRowCountOption = "--postgres-batch-row-count";
    public const string HelpOption = "--help";
    public const string ShortHelpOption = "-h";

    public const string PeakAnalysisMode = "peak-analysis";
    public const string StftAnalysisMode = "stft-analysis";

    public const string DefaultPeakTableName = "T_PeakAnalysis";
    public const string DefaultStftTableName = "T_STFTAnalysis";

    public const string HelpText =
"""
Usage:
  SoundAnalyzer.Cli.exe --window-size <len> --hop <len> --input-dir <path> --mode <peak-analysis|stft-analysis> [--db-file <path> | --postgres ...] [options]

Required options:
  --window-size <len>       Analysis window size (ms/s/m/sample/samples)
  --hop <len>               Hop size (ms/s/m/sample/samples)
  --input-dir <path>        Input root directory
  --mode <value>            peak-analysis or stft-analysis

Storage backend:
  (default: SQLite)
  --db-file <path>          SQLite db file path
  --postgres                Enable PostgreSQL storage backend

PostgreSQL connection options (required with --postgres):
  --postgres-host <host>    PostgreSQL host
  --postgres-port <port>    PostgreSQL port
  --postgres-db <name>      PostgreSQL database name
  --postgres-user <user>    PostgreSQL user
  --postgres-password <pw>  PostgreSQL password (exclusive with ssl cert/key auth)
  --postgres-sslcert-path <path>
                            PostgreSQL client certificate path (requires sslkey-path)
  --postgres-sslkey-path <path>
                            PostgreSQL client private key path (requires sslcert-path)
  --postgres-sslrootcert-path <path>
                            PostgreSQL root CA certificate path (optional)
  --postgres-ssh-host <host>
                            Enable SSH tunnel to PostgreSQL automatically
  --postgres-ssh-port <port>
                            SSH port (default: 22)
  --postgres-ssh-user <user>
                            SSH user (required with postgres-ssh-host)
  --postgres-ssh-private-key-path <path>
                            SSH private key file path (required with postgres-ssh-host)
  --postgres-ssh-known-hosts-path <path>
                            SSH known_hosts file path (required with postgres-ssh-host)
  --postgres-batch-row-count <n>
                            PostgreSQL mode only. Multi-row INSERT batch size (default: 1)

Optional:
  --target-sampling <n>hz   Required when window/hop uses sample(s) in stft mode
  --stems <csv>             Peak mode only. Stem names to analyze (case-insensitive)
  --table-name-override <n> Override table name (default: peak=T_PeakAnalysis, stft=T_STFTAnalysis)
  --upsert                  Upsert by unique key
  --skip-duplicate          Skip duplicates by unique key
  --min-limit-db <dB>       Clamp lower dB bound for all windows/bins (default: -120.0)
  --bin-count <n>           STFT mode only. Number of output bands (required in stft-analysis)
  --delete-current          STFT mode only. Drop current table before processing
  --recursive               STFT mode only. Scan files recursively from input-dir
  --stft-proc-threads <n>   STFT mode only. Processing threads per file (default: 1)
  --peak-proc-threads <n>   Peak mode only. Processing threads per song (default: 1)
  --stft-file-threads <n>   STFT mode only. Number of files analyzed in parallel (default: 1)
  --peak-file-threads <n>   Peak mode only. Number of songs analyzed in parallel (default: 1)
  --insert-queue-size <n>   Bounded queue size between analyze and DB insert (default: 1024)
  --sqlite-fast-mode        SQLite mode only. Enable write-speed PRAGMA tuning (durability trade-off)
  --sqlite-batch-row-count <n>
                            SQLite mode only. Multi-row INSERT batch size (default: 512)
  --ffmpeg-path <path>      ffmpeg executable path or directory containing ffmpeg/ffprobe
  --show-progress           Show advanced progress UI on interactive stderr
  --help, -h                Show help

Examples:
  SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode peak-analysis --stems Piano,Drums --peak-file-threads 2 --peak-proc-threads 4 --insert-queue-size 2048 --show-progress
  SoundAnalyzer.Cli.exe --window-size 2048samples --hop 512samples --target-sampling 44100hz --input-dir /path/to/dir --mode stft-analysis --bin-count 12 --postgres --postgres-host localhost --postgres-port 5432 --postgres-db audio --postgres-user analyzer --postgres-password secret --stft-file-threads 2 --stft-proc-threads 6 --insert-queue-size 4096 --show-progress
""";

    public const string MissingOptionPrefix = "Missing required option: ";
    public const string MissingValuePrefix = "Missing value for option: ";
    public const string UnknownOptionPrefix = "Unknown option: ";
    public const string InvalidTimePrefix = "Invalid length value (ms/s/m/sample/samples, integral required): ";
    public const string InvalidSamplingPrefix = "Invalid sampling value (<positive>hz required): ";
    public const string InvalidNumberPrefix = "Invalid numeric value: ";
    public const string InvalidIntegerPrefix = "Invalid integer value: ";
    public const string InvalidModePrefix = "Unsupported mode: ";
    public const string InvalidTableNamePrefix = "Invalid table name: ";
    public const string InvalidStemsText = "--stems must contain at least one stem name when specified.";
    public const string UpsertSkipConflictText = "--upsert and --skip-duplicate cannot be specified together.";
    public const string SampleUnitOnlyForStftText = "sample/sample(s) units are only supported with --mode stft-analysis.";
    public const string IncompatibleOptionIgnoredPrefix = "Option is ignored for current mode: ";
    public const string PostgresOptionRequiresPostgresModePrefix = "Option requires --postgres mode: ";
    public const string SqliteOptionNotAllowedWithPostgresPrefix = "SQLite option is not allowed with --postgres: ";
    public const string PostgresAuthConflictText = "--postgres-password and --postgres-sslcert-path/--postgres-sslkey-path cannot be used together.";
    public const string PostgresSslPairRequiredText = "--postgres-sslcert-path and --postgres-sslkey-path must be specified together.";
    public const string PostgresDbFileConflictText = "--db-file cannot be used with --postgres.";
    public const string PostgresAuthNotProvidedWarningText = "PostgreSQL auth material is not specified; attempting connection without password/client certificate.";

    public const string InputDirectoryNotFoundPrefix = "Input directory does not exist: ";
    public const string FailedToCreateDbDirectoryPrefix = "Failed to create db directory: ";
    public const string DbFileRequiredPrefix = "SQLite mode requires --db-file: ";
    public const string DuplicateStftAnalysisNamePrefix = "Duplicate analysis name in input set (case-insensitive): ";
    public const string StftBinCountMismatchPrefix = "Existing STFT table bin-count mismatch: ";
    public const string StftSchemaMismatchPrefix = "Existing STFT table schema mismatch: ";
    public const string DatabaseOperationFailedPrefix = "Database operation failed: ";
    public const string PostgresOperationFailedPrefix = "PostgreSQL operation failed: ";
    public const string PostgresCredentialFileNotFoundPrefix = "PostgreSQL credential file was not found: ";
    public const string PostgresSshTunnelFailedPrefix = "SSH tunnel operation failed: ";
    public const string PostgresConfigurationInvalidPrefix = "PostgreSQL configuration is invalid: ";
    public const string OperationCanceledText = "Operation was canceled.";

    public const string FfmpegNotFoundPrefix = "ffmpeg was not found or not executable: ";
    public const string FfprobeNotFoundPrefix = "ffprobe was not found or not executable: ";
    public const string ProbeFailedPrefix = "Failed to probe audio stream: ";
    public const string AnalyzeFailedPrefix = "Failed to analyze windows: ";
    public const string UnsupportedSampleFormatPrefix = "Unsupported input sample format: ";

    public const string PeakInputFileNotFoundPrefix = "Input file does not exist: ";
    public const string PeakInvalidWindowPrefix = "Invalid window-size: ";
    public const string PeakInvalidHopPrefix = "Invalid hop: ";
    public const string PeakInvalidMinLimitPrefix = "Invalid min-limit-db: ";

    public const string StftInputFileNotFoundPrefix = "Input file does not exist: ";
    public const string StftInvalidWindowPrefix = "Invalid window-size: ";
    public const string StftInvalidHopPrefix = "Invalid hop: ";
    public const string StftInvalidBinCountPrefix = "Invalid bin-count: ";
    public const string StftInvalidMinLimitPrefix = "Invalid min-limit-db: ";
    public const string StftInvalidTargetSamplingPrefix = "Invalid target-sampling: ";

    public static string WithValue(string prefix, string value)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat(prefix, value);
    }

    public static string WithInvariantValue(string prefix, double value)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return string.Concat(prefix, value.ToString(CultureInfo.InvariantCulture));
    }
}
