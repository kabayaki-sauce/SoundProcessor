namespace SFFTAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class SfftAnalysisException : Exception
{
    public SfftAnalysisException()
        : this(SfftAnalysisErrorCode.None, string.Empty)
    {
    }

    public SfftAnalysisException(string message)
        : this(SfftAnalysisErrorCode.None, message)
    {
    }

    public SfftAnalysisException(string message, Exception innerException)
        : this(SfftAnalysisErrorCode.None, message, innerException)
    {
    }

    public SfftAnalysisException(SfftAnalysisErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public SfftAnalysisException(SfftAnalysisErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public SfftAnalysisErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
