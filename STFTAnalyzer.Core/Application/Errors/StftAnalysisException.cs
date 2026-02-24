namespace STFTAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class StftAnalysisException : Exception
{
    public StftAnalysisException()
        : this(StftAnalysisErrorCode.None, string.Empty)
    {
    }

    public StftAnalysisException(string message)
        : this(StftAnalysisErrorCode.None, message)
    {
    }

    public StftAnalysisException(string message, Exception innerException)
        : this(StftAnalysisErrorCode.None, message, innerException)
    {
    }

    public StftAnalysisException(StftAnalysisErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public StftAnalysisException(StftAnalysisErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public StftAnalysisErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
