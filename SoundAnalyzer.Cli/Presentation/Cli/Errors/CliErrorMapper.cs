using AudioProcessor.Application.Errors;
using Microsoft.Data.Sqlite;
using PeakAnalyzer.Core.Application.Errors;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Presentation.Cli.Errors;

internal static class CliErrorMapper
{
    public static string ToMessage(CliException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.ErrorCode switch
        {
            CliErrorCode.InputDirectoryNotFound => ConsoleTexts.WithValue(ConsoleTexts.InputDirectoryNotFoundPrefix, exception.Detail),
            CliErrorCode.DbDirectoryCreationFailed => ConsoleTexts.WithValue(ConsoleTexts.FailedToCreateDbDirectoryPrefix, exception.Detail),
            _ => exception.Detail,
        };
    }

    public static string ToMessage(PeakAnalysisException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.ErrorCode switch
        {
            PeakAnalysisErrorCode.InputFileNotFound => ConsoleTexts.WithValue(ConsoleTexts.PeakInputFileNotFoundPrefix, exception.Detail),
            PeakAnalysisErrorCode.InvalidWindowSize => ConsoleTexts.WithValue(ConsoleTexts.PeakInvalidWindowPrefix, exception.Detail),
            PeakAnalysisErrorCode.InvalidHop => ConsoleTexts.WithValue(ConsoleTexts.PeakInvalidHopPrefix, exception.Detail),
            PeakAnalysisErrorCode.InvalidMinLimitDb => ConsoleTexts.WithValue(ConsoleTexts.PeakInvalidMinLimitPrefix, exception.Detail),
            _ => exception.Detail,
        };
    }

    public static string ToMessage(AudioProcessorException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.ErrorCode switch
        {
            AudioProcessorErrorCode.FfmpegNotFound => ConsoleTexts.WithValue(ConsoleTexts.FfmpegNotFoundPrefix, exception.Detail),
            AudioProcessorErrorCode.FfprobeNotFound => ConsoleTexts.WithValue(ConsoleTexts.FfprobeNotFoundPrefix, exception.Detail),
            AudioProcessorErrorCode.ProbeFailed => ConsoleTexts.WithValue(ConsoleTexts.ProbeFailedPrefix, exception.Detail),
            AudioProcessorErrorCode.FrameReadFailed => ConsoleTexts.WithValue(ConsoleTexts.AnalyzeFailedPrefix, exception.Detail),
            AudioProcessorErrorCode.UnsupportedSampleFormat => ConsoleTexts.WithValue(ConsoleTexts.UnsupportedSampleFormatPrefix, exception.Detail),
            _ => exception.Detail,
        };
    }

    public static string ToMessage(SqliteException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ConsoleTexts.WithValue(ConsoleTexts.DatabaseOperationFailedPrefix, exception.Message);
    }
}
