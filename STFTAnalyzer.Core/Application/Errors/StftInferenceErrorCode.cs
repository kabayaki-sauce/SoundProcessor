namespace STFTAnalyzer.Core.Application.Errors;

public enum StftInferenceErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidInputWaveform,
    InvalidConfiguration,
    ProbeFailed,
    FrameReadFailed,
}
