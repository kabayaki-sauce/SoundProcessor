namespace MelSpectrogramAnalyzer.Core.Application.Errors;

#pragma warning disable S3871
public sealed class MelSpectrogramInferenceException : Exception
{
    public MelSpectrogramInferenceException()
        : this(MelSpectrogramInferenceErrorCode.None, string.Empty)
    {
    }

    public MelSpectrogramInferenceException(string message)
        : this(MelSpectrogramInferenceErrorCode.None, message)
    {
    }

    public MelSpectrogramInferenceException(string message, Exception innerException)
        : this(MelSpectrogramInferenceErrorCode.None, message, innerException)
    {
    }

    public MelSpectrogramInferenceException(MelSpectrogramInferenceErrorCode errorCode, string detail)
        : base(detail)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public MelSpectrogramInferenceException(
        MelSpectrogramInferenceErrorCode errorCode,
        string detail,
        Exception innerException)
        : base(detail, innerException)
    {
        ErrorCode = errorCode;
        Detail = detail;
    }

    public MelSpectrogramInferenceErrorCode ErrorCode { get; }

    public string Detail { get; }
}
#pragma warning restore S3871
