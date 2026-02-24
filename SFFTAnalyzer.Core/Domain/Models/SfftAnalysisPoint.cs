namespace SFFTAnalyzer.Core.Domain.Models;

public sealed class SfftAnalysisPoint
{
    public SfftAnalysisPoint(
        string name,
        int channel,
        long windowMs,
        long ms,
        IReadOnlyList<double> bins)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ms);
        ArgumentNullException.ThrowIfNull(bins);

        if (bins.Count == 0)
        {
            throw new ArgumentException("At least one bin is required.", nameof(bins));
        }

        for (int i = 0; i < bins.Count; i++)
        {
            double value = bins[i];
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(bins));
            }
        }

        Name = name;
        Channel = channel;
        WindowMs = windowMs;
        Ms = ms;
        Bins = bins;
    }

    public string Name { get; }

    public int Channel { get; }

    public long WindowMs { get; }

    public long Ms { get; }

    public IReadOnlyList<double> Bins { get; }
}
