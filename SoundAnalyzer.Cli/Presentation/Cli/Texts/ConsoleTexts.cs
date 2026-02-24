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
    public const string FfmpegPathOption = "--ffmpeg-path";
    public const string ProgressOption = "--progress";
    public const string HelpOption = "--help";
    public const string ShortHelpOption = "-h";

    public const string PeakAnalysisMode = "peak-analysis";
    public const string StftAnalysisMode = "stft-analysis";

    public const string DefaultPeakTableName = "T_PeakAnalysis";
    public const string DefaultStftTableName = "T_STFTAnalysis";

    public const string HelpText =
"""
Usage:
  SoundAnalyzer.Cli.exe --window-size <len> --hop <len> --input-dir <path> --db-file <path> --mode <peak-analysis|stft-analysis> [options]

Required options:
  --window-size <len>       Analysis window size (ms/s/m/sample/samples)
  --hop <len>               Hop size (ms/s/m/sample/samples)
  --input-dir <path>        Input root directory
  --db-file <path>          SQLite db file path
  --mode <value>            peak-analysis or stft-analysis

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
  --ffmpeg-path <path>      ffmpeg executable path or directory containing ffmpeg/ffprobe
  --progress                Show two-line progress bars on interactive stderr
  --help, -h                Show help

Examples:
  SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode peak-analysis --stems Piano,Drums --table-name-override T_PEAK --upsert
  SoundAnalyzer.Cli.exe --window-size 2048samples --hop 512samples --target-sampling 44100hz --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 12 --table-name-override T_STFT --upsert --recursive
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
    public const string BinCountOnlyForStftText = "--bin-count can only be used with --mode stft-analysis.";
    public const string DeleteCurrentOnlyForStftText = "--delete-current can only be used with --mode stft-analysis.";
    public const string RecursiveOnlyForStftText = "--recursive can only be used with --mode stft-analysis.";
    public const string StemsNotSupportedForStftText = "--stems is not supported with --mode stft-analysis.";
    public const string SampleUnitOnlyForStftText = "sample/sample(s) units are only supported with --mode stft-analysis.";
    public const string TargetSamplingOnlyForStftText = "--target-sampling can only be used with --mode stft-analysis when sample/sample(s) units are used.";

    public const string InputDirectoryNotFoundPrefix = "Input directory does not exist: ";
    public const string FailedToCreateDbDirectoryPrefix = "Failed to create db directory: ";
    public const string DuplicateStftAnalysisNamePrefix = "Duplicate analysis name in input set (case-insensitive): ";
    public const string StftBinCountMismatchPrefix = "Existing STFT table bin-count mismatch: ";
    public const string StftSchemaMismatchPrefix = "Existing STFT table schema mismatch: ";
    public const string DatabaseOperationFailedPrefix = "Database operation failed: ";
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
