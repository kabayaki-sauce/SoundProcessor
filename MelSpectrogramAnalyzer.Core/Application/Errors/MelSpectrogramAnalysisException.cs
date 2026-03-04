namespace MelSpectrogramAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class MelSpectrogramAnalysisException : Exception
{
    public MelSpectrogramAnalysisException()
        : this(MelSpectrogramAnalysisErrorCode.None, string.Empty)
    {
    }

    public MelSpectrogramAnalysisException(string message)
        : this(MelSpectrogramAnalysisErrorCode.None, message)
    {
    }

    public MelSpectrogramAnalysisException(string message, Exception innerException)
        : this(MelSpectrogramAnalysisErrorCode.None, message, innerException)
    {
    }

    public MelSpectrogramAnalysisException(MelSpectrogramAnalysisErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public MelSpectrogramAnalysisException(MelSpectrogramAnalysisErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public MelSpectrogramAnalysisErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871

