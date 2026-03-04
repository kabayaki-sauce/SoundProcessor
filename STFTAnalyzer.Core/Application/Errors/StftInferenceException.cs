namespace STFTAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class StftInferenceException : Exception
{
    public StftInferenceException()
        : this(StftInferenceErrorCode.None, string.Empty)
    {
    }

    public StftInferenceException(string message)
        : this(StftInferenceErrorCode.None, message)
    {
    }

    public StftInferenceException(string message, Exception innerException)
        : this(StftInferenceErrorCode.None, message, innerException)
    {
    }

    public StftInferenceException(StftInferenceErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public StftInferenceException(StftInferenceErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public StftInferenceErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
