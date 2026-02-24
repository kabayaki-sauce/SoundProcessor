using System.Globalization;

namespace AudioSplitter.Cli.Presentation.Cli.Texts;

internal static class ConsoleTexts
{
    public const string InputFileOption = "--input-file";
    public const string InputDirOption = "--input-dir";
    public const string OutputDirOption = "--output-dir";
    public const string LevelOption = "--level";
    public const string DurationOption = "--duration";
    public const string AfterOffsetOption = "--after-offset";
    public const string ResumeOffsetOption = "--resume-offset";
    public const string ResolutionTypeOption = "--resolution-type";
    public const string FfmpegPathOption = "--ffmpeg-path";
    public const string RecursiveOption = "--recursive";
    public const string OverwriteOption = "-y";
    public const string HelpOption = "--help";
    public const string ShortHelpOption = "-h";

    public const string YesAnswer = "y";
    public const string YesLongAnswer = "yes";
    public const string NoAnswer = "n";
    public const string NoLongAnswer = "no";

    public const string HelpText =
"""
Usage:
  AudioSplitter.Cli.exe (--input-file <path> | --input-dir <path>) --output-dir <path> --level <dBFS> --duration <time> [options]

Required options:
  --input-file <path>       Input audio file path (wav/flac/m4a/caf)
  --input-dir <path>        Input directory path
  --output-dir <path>       Output directory path
  --level <dBFS>            Silence threshold in dBFS, negative value only
  --duration <time>         Silence duration threshold (e.g. 2000ms, 2s, 1m)

Optional:
  --recursive               Scan input directory recursively (available with --input-dir)
  --after-offset <time>     Keep this duration from silence start in previous segment (default: 0ms)
  --resume-offset <time>    Offset from next sound start for next segment, negative allowed (default: 0ms)
  --resolution-type <spec>  Output resolution: 16bit|24bit|32float,<rate>hz
  --ffmpeg-path <path>      ffmpeg executable path or directory containing ffmpeg/ffprobe
  -y                        Overwrite output without confirmation
  --help, -h                Show help
""";

    public const string MissingOptionPrefix = "Missing required option: ";
    public const string MissingValuePrefix = "Missing value for option: ";
    public const string UnknownOptionPrefix = "Unknown option: ";
    public const string InvalidNumberPrefix = "Invalid numeric value: ";
    public const string InvalidTimePrefix = "Invalid time value (ms/s/m): ";
    public const string InvalidResolutionPrefix = "Invalid resolution-type format: ";
    public const string InvalidLevelText = "--level must be lower than 0.";
    public const string InvalidDurationText = "--duration must be greater than 0.";
    public const string InvalidAfterOffsetText = "--after-offset must be 0 or greater.";
    public const string InputSourceRequiredText = "Specify exactly one of --input-file or --input-dir.";
    public const string InputSourceExclusiveText = "--input-file and --input-dir cannot be specified together.";
    public const string RecursiveRequiresInputDirText = "--recursive can only be used with --input-dir.";
    public const string InputFileNotFoundPrefix = "Input file does not exist: ";
    public const string InputDirectoryNotFoundPrefix = "Input directory does not exist: ";
    public const string OutputDirectoryCreationFailedPrefix = "Failed to create output directory: ";
    public const string FfmpegNotFoundPrefix = "ffmpeg was not found or not executable: ";
    public const string FfprobeNotFoundPrefix = "ffprobe was not found or not executable: ";
    public const string ProbeFailedPrefix = "Failed to probe audio stream: ";
    public const string AnalyzeFailedPrefix = "Failed to analyze silence: ";
    public const string ExportFailedPrefix = "Failed to export segment: ";
    public const string UnsupportedSampleFormatPrefix = "Unsupported input sample format: ";
    public const string OverwritePromptSuffix = " already exists. Overwrite? [y/N]: ";
    public const string OverwriteAnswerInvalid = "Please answer y or n.";
    public const string OverwriteConflictInNonInteractive = "Overwrite confirmation is required but no interactive terminal is available. Use -y.";
    public const string UnexpectedErrorPrefix = "Unexpected error: ";
    public const string OperationCanceledText = "Operation was canceled.";
    public const string IncompleteFrameData = "Incomplete frame data detected while analyzing stream.";

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

    public static string PathWithSuffix(string path, string suffix)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(suffix);
        return string.Concat(path, suffix);
    }
}
