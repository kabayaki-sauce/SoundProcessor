namespace SoundAnalyzer.Cli.Infrastructure.Execution;

#pragma warning disable S3871
internal sealed class CliException : Exception
{
    public CliException()
        : this(CliErrorCode.None, string.Empty)
    {
    }

    public CliException(string message)
        : this(CliErrorCode.None, message)
    {
    }

    public CliException(string message, Exception innerException)
        : this(CliErrorCode.None, message, innerException)
    {
    }

    public CliException(CliErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public CliException(CliErrorCode errorCode, string detail, Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public CliErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
