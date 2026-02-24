namespace STFTAnalyzer.Core.Domain.Models;

public sealed class StftAnalysisPoint
{
    public StftAnalysisPoint(
        string name,
        int channel,
        long window,
        long anchor,
        IReadOnlyList<double> bins)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(window);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(anchor);
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
        Window = window;
        Anchor = anchor;
        Bins = bins;
    }

    public string Name { get; }

    public int Channel { get; }

    public long Window { get; }

    public long Anchor { get; }

    public IReadOnlyList<double> Bins { get; }
}
