#pragma warning disable CA1814
#pragma warning disable CA1819
#pragma warning disable S2368
namespace STFTAnalyzer.Core.Domain.Models;

public sealed class StftInferenceFramePoint
{
    public StftInferenceFramePoint(
        string name,
        int nowMs,
        int frameIndex,
        double[,]? linear,
        double[,]? db)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(nowMs);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);

        if (linear is null && db is null)
        {
            throw new ArgumentException("Either linear or db output must be provided.");
        }

        ValidateMatrix(linear, allowNegativeInfinity: false);
        ValidateMatrix(db, allowNegativeInfinity: true);
        ValidateShapeCompatibility(linear, db);

        Name = name;
        NowMs = nowMs;
        FrameIndex = frameIndex;
        Linear = linear;
        Db = db;
    }

    public string Name { get; }

    public int NowMs { get; }

    public int FrameIndex { get; }

    public double[,]? Linear { get; }

    public double[,]? Db { get; }

    public int Channels => Linear?.GetLength(0) ?? Db!.GetLength(0);

    public int FrequencyBins => Linear?.GetLength(1) ?? Db!.GetLength(1);

    private static void ValidateShapeCompatibility(double[,]? linear, double[,]? db)
    {
        if (linear is null || db is null)
        {
            return;
        }

        bool shapeMatches = linear.GetLength(0) == db.GetLength(0)
            && linear.GetLength(1) == db.GetLength(1);
        if (!shapeMatches)
        {
            throw new ArgumentException("linear and db matrix shape mismatch.");
        }
    }

    private static void ValidateMatrix(double[,]? matrix, bool allowNegativeInfinity)
    {
        if (matrix is null)
        {
            return;
        }

        if (matrix.GetLength(0) <= 0 || matrix.GetLength(1) <= 0)
        {
            throw new ArgumentException("Matrix dimensions must be greater than zero.");
        }

        for (int ch = 0; ch < matrix.GetLength(0); ch++)
        {
            for (int freq = 0; freq < matrix.GetLength(1); freq++)
            {
                double value = matrix[ch, freq];
                if (double.IsNaN(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(matrix), "Matrix must not contain NaN.");
                }

                if (double.IsInfinity(value))
                {
                    bool isAllowedNegativeInfinity = allowNegativeInfinity && double.IsNegativeInfinity(value);
                    if (!isAllowedNegativeInfinity)
                    {
                        throw new ArgumentOutOfRangeException(nameof(matrix), "Matrix contains unsupported infinity.");
                    }
                }
            }
        }
    }
}
#pragma warning restore S2368
#pragma warning restore CA1819
#pragma warning restore CA1814
