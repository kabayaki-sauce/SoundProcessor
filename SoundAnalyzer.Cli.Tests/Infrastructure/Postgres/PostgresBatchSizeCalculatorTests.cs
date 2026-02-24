using SoundAnalyzer.Cli.Infrastructure.Postgres;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Postgres;

public sealed class PostgresBatchSizeCalculatorTests
{
    [Fact]
    public void ResolveEffectiveBatchRowCount_ShouldReturnRequested_WhenWithinLimit()
    {
        int actual = PostgresBatchSizeCalculator.ResolveEffectiveBatchRowCount(
            requestedBatchRowCount: 5_000,
            columnsPerRow: 8);

        Assert.Equal(5_000, actual);
    }

    [Fact]
    public void ResolveEffectiveBatchRowCount_ShouldClampByParameterLimit_ForPeakColumns()
    {
        int actual = PostgresBatchSizeCalculator.ResolveEffectiveBatchRowCount(
            requestedBatchRowCount: 100_000,
            columnsPerRow: 7);

        Assert.Equal(9_362, actual);
    }

    [Fact]
    public void ResolveEffectiveBatchRowCount_ShouldClampByParameterLimit_ForStftColumns()
    {
        int actual = PostgresBatchSizeCalculator.ResolveEffectiveBatchRowCount(
            requestedBatchRowCount: 100_000,
            columnsPerRow: 8);

        Assert.Equal(8_191, actual);
    }

    [Fact]
    public void ResolveEffectiveBatchRowCount_ShouldThrow_WhenRequestedBatchRowCountIsNotPositive()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PostgresBatchSizeCalculator.ResolveEffectiveBatchRowCount(
                requestedBatchRowCount: 0,
                columnsPerRow: 8));
    }

    [Fact]
    public void ResolveEffectiveBatchRowCount_ShouldThrow_WhenColumnsPerRowIsNotPositive()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PostgresBatchSizeCalculator.ResolveEffectiveBatchRowCount(
                requestedBatchRowCount: 1,
                columnsPerRow: 0));
    }
}
