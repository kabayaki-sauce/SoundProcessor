namespace MelSpectrogramAnalyzer.Core.Application.Errors;

public enum MelSpectrogramInferenceErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidInputWaveform,
    InvalidConfiguration,
    ProbeFailed,
    FrameReadFailed,
}
