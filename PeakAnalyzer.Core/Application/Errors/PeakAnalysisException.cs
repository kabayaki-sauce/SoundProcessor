namespace PeakAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class PeakAnalysisException : Exception
{
    public PeakAnalysisException()
        : this(PeakAnalysisErrorCode.None, string.Empty)
    {
    }

    public PeakAnalysisException(string message)
        : this(PeakAnalysisErrorCode.None, message)
    {
    }

    public PeakAnalysisException(string message, Exception innerException)
        : this(PeakAnalysisErrorCode.None, message, innerException)
    {
    }

    public PeakAnalysisException(PeakAnalysisErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public PeakAnalysisException(PeakAnalysisErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public PeakAnalysisErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
