using AudioProcessor.Application.Errors;
using AudioSplitter.Core.Application.Errors;
using AudioSplitter.Cli.Presentation.Cli.Texts;

namespace AudioSplitter.Cli.Presentation.Cli.Errors;

internal static class CliErrorMapper
{
    public static string ToMessage(SplitAudioException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.ErrorCode switch
        {
            SplitAudioErrorCode.InputFileNotFound => ConsoleTexts.WithValue(ConsoleTexts.InputFileNotFoundPrefix, exception.Detail),
            SplitAudioErrorCode.OutputDirectoryCreationFailed => ConsoleTexts.WithValue(ConsoleTexts.OutputDirectoryCreationFailedPrefix, exception.Detail),
            SplitAudioErrorCode.OverwriteConflictInNonInteractive => ConsoleTexts.OverwriteConflictInNonInteractive,
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
            AudioProcessorErrorCode.UnsupportedSampleFormat => ConsoleTexts.WithValue(ConsoleTexts.UnsupportedSampleFormatPrefix, exception.Detail),
            AudioProcessorErrorCode.FrameReadFailed => ConsoleTexts.WithValue(ConsoleTexts.AnalyzeFailedPrefix, exception.Detail),
            AudioProcessorErrorCode.IncompleteFrameData => ConsoleTexts.IncompleteFrameData,
            AudioProcessorErrorCode.ExportFailed => ConsoleTexts.WithValue(ConsoleTexts.ExportFailedPrefix, exception.Detail),
            _ => exception.Detail,
        };
    }
}
