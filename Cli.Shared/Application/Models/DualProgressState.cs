namespace Cli.Shared.Application.Models;

public readonly record struct DualProgressState(
    string TopLabel,
    double TopRatio,
    string BottomLabel,
    double BottomRatio)
{
    public string TopLabel { get; } = string.IsNullOrWhiteSpace(TopLabel) ? "Progress" : TopLabel;

    public double TopRatio { get; } = TopRatio;

    public string BottomLabel { get; } = string.IsNullOrWhiteSpace(BottomLabel) ? "Current" : BottomLabel;

    public double BottomRatio { get; } = BottomRatio;
}
