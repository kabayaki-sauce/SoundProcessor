namespace STFTAnalyzer.Core.Application.Errors;

public enum StftAnalysisErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidWindowSize,
    InvalidHop,
    InvalidBinCount,
    InvalidMinLimitDb,
    InvalidTargetSampling,
}
