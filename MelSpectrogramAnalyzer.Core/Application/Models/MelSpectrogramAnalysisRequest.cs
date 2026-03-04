namespace MelSpectrogramAnalyzer.Core.Application.Models;

public sealed class MelSpectrogramAnalysisRequest
{
    public MelSpectrogramAnalysisRequest(
        string inputFilePath,
        string name,
        long windowSamples,
        long hopSamples,
        int analysisSampleRate,
        MelSpectrogramAnchorUnit anchorUnit,
        long windowPersistedValue,
        int melBinCount,
        double melFminHz,
        double melFmaxHz,
        MelSpectrogramScaleKind melScaleKind,
        int melPower,
        double minLimitDb,
        string? ffmpegPath,
        int processingThreads = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(analysisSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowPersistedValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melBinCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingThreads);

        if (double.IsNaN(melFminHz) || double.IsInfinity(melFminHz) || melFminHz < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(melFminHz));
        }

        if (double.IsNaN(melFmaxHz) || double.IsInfinity(melFmaxHz) || melFmaxHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(melFmaxHz));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(melFmaxHz, melFminHz);

        if (melPower is not 1 and not 2)
        {
            throw new ArgumentOutOfRangeException(nameof(melPower));
        }

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        InputFilePath = inputFilePath;
        Name = name;
        WindowSamples = windowSamples;
        HopSamples = hopSamples;
        AnalysisSampleRate = analysisSampleRate;
        AnchorUnit = anchorUnit;
        WindowPersistedValue = windowPersistedValue;
        MelBinCount = melBinCount;
        MelFminHz = melFminHz;
        MelFmaxHz = melFmaxHz;
        MelScaleKind = melScaleKind;
        MelPower = melPower;
        MinLimitDb = minLimitDb;
        FfmpegPath = ffmpegPath;
        ProcessingThreads = processingThreads;
    }

    public string InputFilePath { get; }

    public string Name { get; }

    public long WindowSamples { get; }

    public long HopSamples { get; }

    public int AnalysisSampleRate { get; }

    public MelSpectrogramAnchorUnit AnchorUnit { get; }

    public long WindowPersistedValue { get; }

    public int MelBinCount { get; }

    public double MelFminHz { get; }

    public double MelFmaxHz { get; }

    public MelSpectrogramScaleKind MelScaleKind { get; }

    public int MelPower { get; }

    public double MinLimitDb { get; }

    public string? FfmpegPath { get; }

    public int ProcessingThreads { get; }
}

