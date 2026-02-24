namespace PeakAnalyzer.Core.Domain.Models;

public sealed class PeakAnalysisPoint
{
    public PeakAnalysisPoint(string name, string stem, long windowMs, long ms, double db)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ms);

        if (double.IsNaN(db) || double.IsInfinity(db))
        {
            throw new ArgumentOutOfRangeException(nameof(db));
        }

        Name = name;
        Stem = stem;
        WindowMs = windowMs;
        Ms = ms;
        Db = db;
    }

    public string Name { get; }

    public string Stem { get; }

    public long WindowMs { get; }

    public long Ms { get; }

    public double Db { get; }
}
