namespace AudioProcessor.Application.Errors;

public enum AudioProcessorErrorCode
{
    None = 0,
    FfmpegNotFound,
    FfprobeNotFound,
    ProbeFailed,
    UnsupportedSampleFormat,
    FrameReadFailed,
    IncompleteFrameData,
    ExportFailed,
}
