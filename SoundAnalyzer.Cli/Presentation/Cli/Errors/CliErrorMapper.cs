using AudioProcessor.Application.Errors;
using Microsoft.Data.Sqlite;
using Npgsql;
using PeakAnalyzer.Core.Application.Errors;
using Renci.SshNet.Common;
using STFTAnalyzer.Core.Application.Errors;
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
            CliErrorCode.DbFileRequired => ConsoleTexts.WithValue(ConsoleTexts.DbFileRequiredPrefix, exception.Detail),
            CliErrorCode.DuplicateStftAnalysisName => ConsoleTexts.WithValue(ConsoleTexts.DuplicateStftAnalysisNamePrefix, exception.Detail),
            CliErrorCode.StftTableBinCountMismatch => ConsoleTexts.WithValue(ConsoleTexts.StftBinCountMismatchPrefix, exception.Detail),
            CliErrorCode.StftTableSchemaMismatch => ConsoleTexts.WithValue(ConsoleTexts.StftSchemaMismatchPrefix, exception.Detail),
            CliErrorCode.PostgresConfigurationInvalid => ConsoleTexts.WithValue(ConsoleTexts.PostgresConfigurationInvalidPrefix, exception.Detail),
            CliErrorCode.PostgresCredentialFileNotFound => ConsoleTexts.WithValue(ConsoleTexts.PostgresCredentialFileNotFoundPrefix, exception.Detail),
            CliErrorCode.PostgresSshTunnelFailed => ConsoleTexts.WithValue(ConsoleTexts.PostgresSshTunnelFailedPrefix, exception.Detail),
            CliErrorCode.UnsupportedMode => ConsoleTexts.WithValue(ConsoleTexts.InvalidModePrefix, exception.Detail),
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

    public static string ToMessage(StftAnalysisException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.ErrorCode switch
        {
            StftAnalysisErrorCode.InputFileNotFound => ConsoleTexts.WithValue(ConsoleTexts.StftInputFileNotFoundPrefix, exception.Detail),
            StftAnalysisErrorCode.InvalidWindowSize => ConsoleTexts.WithValue(ConsoleTexts.StftInvalidWindowPrefix, exception.Detail),
            StftAnalysisErrorCode.InvalidHop => ConsoleTexts.WithValue(ConsoleTexts.StftInvalidHopPrefix, exception.Detail),
            StftAnalysisErrorCode.InvalidBinCount => ConsoleTexts.WithValue(ConsoleTexts.StftInvalidBinCountPrefix, exception.Detail),
            StftAnalysisErrorCode.InvalidMinLimitDb => ConsoleTexts.WithValue(ConsoleTexts.StftInvalidMinLimitPrefix, exception.Detail),
            StftAnalysisErrorCode.InvalidTargetSampling => ConsoleTexts.WithValue(ConsoleTexts.StftInvalidTargetSamplingPrefix, exception.Detail),
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

    public static string ToMessage(NpgsqlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ConsoleTexts.WithValue(ConsoleTexts.PostgresOperationFailedPrefix, exception.Message);
    }

    public static string ToMessage(SshException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ConsoleTexts.WithValue(ConsoleTexts.PostgresSshTunnelFailedPrefix, exception.Message);
    }
}
