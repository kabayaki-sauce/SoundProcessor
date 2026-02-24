namespace AudioProcessor.Application.Errors;

#pragma warning disable S3871
public sealed class AudioProcessorException : Exception
{
    public AudioProcessorException()
        : this(AudioProcessorErrorCode.None, string.Empty)
    {
    }

    public AudioProcessorException(string message)
        : this(AudioProcessorErrorCode.None, message)
    {
    }

    public AudioProcessorException(string message, Exception innerException)
        : this(AudioProcessorErrorCode.None, message, innerException)
    {
    }

    public AudioProcessorException(AudioProcessorErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public AudioProcessorException(AudioProcessorErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public AudioProcessorErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
