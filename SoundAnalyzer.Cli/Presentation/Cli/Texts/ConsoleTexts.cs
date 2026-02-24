using System.Globalization;

namespace SoundAnalyzer.Cli.Presentation.Cli.Texts;

internal static class ConsoleTexts
{
    public const string WindowSizeOption = "--window-size";
    public const string HopOption = "--hop";
    public const string InputDirOption = "--input-dir";
    public const string DbFileOption = "--db-file";
    public const string StemsOption = "--stems";
    public const string ModeOption = "--mode";
    public const string TableNameOverrideOption = "--table-name-override";
    public const string UpsertOption = "--upsert";
    public const string SkipDuplicateOption = "--skip-duplicate";
    public const string MinLimitDbOption = "--min-limit-db";
    public const string FfmpegPathOption = "--ffmpeg-path";
    public const string HelpOption = "--help";
    public const string ShortHelpOption = "-h";

    public const string PeakAnalysisMode = "peak-analysis";
    public const string DefaultTableName = "T_PeakAnalysis";

    public const string HelpText =
"""
Usage:
  SoundAnalyzer.Cli.exe --window-size <time> --hop <time> --input-dir <path> --db-file <path> --mode peak-analysis [options]

Required options:
  --window-size <time>      Analysis window size (ms/s/m), integral milliseconds only
  --hop <time>              Hop size (ms/s/m), integral milliseconds only
  --input-dir <path>        Input root directory (only first-level subdirectories are scanned)
  --db-file <path>          SQLite db file path
  --mode <value>            Only peak-analysis is supported

Optional:
  --stems <csv>             Stem names to analyze (case-insensitive). Omit to analyze all stems.
  --table-name-override <n> Override table name (default: T_PeakAnalysis)
  --upsert                  Upsert by unique key (name, stem, window, ms)
  --skip-duplicate          Skip duplicates by unique key (name, stem, window, ms)
  --min-limit-db <dB>       Clamp lower dB bound for all windows (default: -120.0)
  --ffmpeg-path <path>      ffmpeg executable path or directory containing ffmpeg/ffprobe
  --help, -h                Show help

Example:
  SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --stems Piano,Drums,Vocals --mode peak-analysis --table-name-override T_PEAK --upsert
""";

    public const string MissingOptionPrefix = "Missing required option: ";
    public const string MissingValuePrefix = "Missing value for option: ";
    public const string UnknownOptionPrefix = "Unknown option: ";
    public const string InvalidTimePrefix = "Invalid time value (ms/s/m, integral ms required): ";
    public const string InvalidNumberPrefix = "Invalid numeric value: ";
    public const string InvalidModePrefix = "Unsupported mode: ";
    public const string InvalidTableNamePrefix = "Invalid table name: ";
    public const string InvalidStemsText = "--stems must contain at least one stem name when specified.";
    public const string UpsertSkipConflictText = "--upsert and --skip-duplicate cannot be specified together.";

    public const string InputDirectoryNotFoundPrefix = "Input directory does not exist: ";
    public const string FailedToCreateDbDirectoryPrefix = "Failed to create db directory: ";
    public const string DatabaseOperationFailedPrefix = "Database operation failed: ";
    public const string OperationCanceledText = "Operation was canceled.";

    public const string FfmpegNotFoundPrefix = "ffmpeg was not found or not executable: ";
    public const string FfprobeNotFoundPrefix = "ffprobe was not found or not executable: ";
    public const string ProbeFailedPrefix = "Failed to probe audio stream: ";
    public const string AnalyzeFailedPrefix = "Failed to analyze peak windows: ";
    public const string UnsupportedSampleFormatPrefix = "Unsupported input sample format: ";

    public const string PeakInputFileNotFoundPrefix = "Input file does not exist: ";
    public const string PeakInvalidWindowPrefix = "Invalid window-size: ";
    public const string PeakInvalidHopPrefix = "Invalid hop: ";
    public const string PeakInvalidMinLimitPrefix = "Invalid min-limit-db: ";

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
