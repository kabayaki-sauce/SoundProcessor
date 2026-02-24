namespace PeakAnalyzer.Core.Application.Models;

public sealed class PeakAnalysisRequest
{
    public PeakAnalysisRequest(
        string inputFilePath,
        string name,
        string stem,
        long windowSizeMs,
        long hopMs,
        double minLimitDb,
        string? ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSizeMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);

        if (double.IsNaN(minLimitDb) || double.IsInfinity(minLimitDb))
        {
            throw new ArgumentOutOfRangeException(nameof(minLimitDb));
        }

        InputFilePath = inputFilePath;
        Name = name;
        Stem = stem;
        WindowSizeMs = windowSizeMs;
        HopMs = hopMs;
        MinLimitDb = minLimitDb;
        FfmpegPath = ffmpegPath;
    }

    public string InputFilePath { get; }

    public string Name { get; }

    public string Stem { get; }

    public long WindowSizeMs { get; }

    public long HopMs { get; }

    public double MinLimitDb { get; }

    public string? FfmpegPath { get; }
}
