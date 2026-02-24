namespace SFFTAnalyzer.Core.Application.Errors;

public enum SfftAnalysisErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidWindowSize,
    InvalidHop,
    InvalidBinCount,
    InvalidMinLimitDb,
}
