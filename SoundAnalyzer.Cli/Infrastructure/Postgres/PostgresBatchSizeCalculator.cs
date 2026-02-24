namespace SoundAnalyzer.Cli.Infrastructure.Postgres;

internal static class PostgresBatchSizeCalculator
{
    private const int MaxParameterCount = 65_535;

    public static int ResolveEffectiveBatchRowCount(int requestedBatchRowCount, int columnsPerRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedBatchRowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnsPerRow);

        int maxRowsByParameterLimit = Math.Max(1, MaxParameterCount / columnsPerRow);
        return Math.Min(requestedBatchRowCount, maxRowsByParameterLimit);
    }
}
