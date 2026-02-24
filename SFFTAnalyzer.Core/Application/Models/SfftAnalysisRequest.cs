namespace SFFTAnalyzer.Core.Application.Models;

public sealed class SfftAnalysisRequest
{
    public SfftAnalysisRequest(
        string inputFilePath,
        string name,
        long windowSizeMs,
        long hopMs,
        int binCount,
        double minLimitDb,
        string? ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSizeMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        InputFilePath = inputFilePath;
        Name = name;
        WindowSizeMs = windowSizeMs;
        HopMs = hopMs;
        BinCount = binCount;
        MinLimitDb = minLimitDb;
        FfmpegPath = ffmpegPath;
    }

    public string InputFilePath { get; }

    public string Name { get; }

    public long WindowSizeMs { get; }

    public long HopMs { get; }

    public int BinCount { get; }

    public double MinLimitDb { get; }

    public string? FfmpegPath { get; }
}
