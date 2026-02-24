namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal readonly record struct AnalysisLengthArgument
{
    public AnalysisLengthArgument(long value, AnalysisLengthUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

        Value = value;
        Unit = unit;
    }

    public long Value { get; }

    public AnalysisLengthUnit Unit { get; }

    public bool IsSample => Unit == AnalysisLengthUnit.Sample;
}
