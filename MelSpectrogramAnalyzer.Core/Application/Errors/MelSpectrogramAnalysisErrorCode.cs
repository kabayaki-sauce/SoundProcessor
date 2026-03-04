namespace MelSpectrogramAnalyzer.Core.Application.Errors;

public enum MelSpectrogramAnalysisErrorCode
{
    None = 0,
    InputFileNotFound,
    InvalidWindowSize,
    InvalidHop,
    InvalidMelBinCount,
    InvalidMelFrequencies,
    InvalidMelScale,
    InvalidMelPower,
    InvalidMinLimitDb,
    InvalidTargetSampling,
}

