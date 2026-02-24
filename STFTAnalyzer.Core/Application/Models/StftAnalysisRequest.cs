namespace STFTAnalyzer.Core.Application.Models;

public sealed class StftAnalysisRequest
{
    public StftAnalysisRequest(
        string inputFilePath,
        string name,
        long windowSamples,
        long hopSamples,
        int analysisSampleRate,
        StftAnchorUnit anchorUnit,
        long windowPersistedValue,
        int binCount,
        double minLimitDb,
        string? ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(analysisSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowPersistedValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

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
        BinCount = binCount;
        MinLimitDb = minLimitDb;
        FfmpegPath = ffmpegPath;
    }

    public string InputFilePath { get; }

    public string Name { get; }

    public long WindowSamples { get; }

    public long HopSamples { get; }

    public int AnalysisSampleRate { get; }

    public StftAnchorUnit AnchorUnit { get; }

    public long WindowPersistedValue { get; }

    public int BinCount { get; }

    public double MinLimitDb { get; }

    public string? FfmpegPath { get; }
}
