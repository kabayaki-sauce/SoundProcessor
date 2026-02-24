namespace AudioSplitter.Core.Application.Errors;

#pragma warning disable S3871
public sealed class SplitAudioException : Exception
{
    public SplitAudioException()
        : this(SplitAudioErrorCode.None, string.Empty)
    {
    }

    public SplitAudioException(string message)
        : this(SplitAudioErrorCode.None, message)
    {
    }

    public SplitAudioException(string message, Exception innerException)
        : this(SplitAudioErrorCode.None, message, innerException)
    {
    }

    public SplitAudioException(SplitAudioErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public SplitAudioException(SplitAudioErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public SplitAudioErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
