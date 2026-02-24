namespace PeakAnalyzer.Core.Application.Errors;

public enum PeakAnalysisErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidWindowSize,
    InvalidHop,
    InvalidMinLimitDb,
}
