#pragma warning disable CA1814
#pragma warning disable CA1819
#pragma warning disable S2368
namespace MelSpectrogramAnalyzer.Core.Domain.Models;

public sealed class MelSpectrogramInferenceNowTensorPoint
{
    public MelSpectrogramInferenceNowTensorPoint(
        string name,
        int nowMs,
        double[,,]? linear,
        double[,,]? db)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(nowMs);

        if (linear is null && db is null)
        {
            throw new ArgumentException("Either linear or db output must be provided.");
        }

        ValidateTensor(linear, allowNegativeInfinity: false);
        ValidateTensor(db, allowNegativeInfinity: true);
        ValidateShapeCompatibility(linear, db);

        Name = name;
        NowMs = nowMs;
        Linear = linear;
        Db = db;
    }

    public string Name { get; }

    public int NowMs { get; }

    public double[,,]? Linear { get; }

    public double[,,]? Db { get; }

    public int Channels => Linear?.GetLength(0) ?? Db!.GetLength(0);

    public int MelBins => Linear?.GetLength(1) ?? Db!.GetLength(1);

    public int FrameCount => Linear?.GetLength(2) ?? Db!.GetLength(2);

    private static void ValidateShapeCompatibility(double[,,]? linear, double[,,]? db)
    {
        if (linear is null || db is null)
        {
            return;
        }

        bool shapeMatches = linear.GetLength(0) == db.GetLength(0)
            && linear.GetLength(1) == db.GetLength(1)
            && linear.GetLength(2) == db.GetLength(2);
        if (!shapeMatches)
        {
            throw new ArgumentException("linear and db tensor shape mismatch.");
        }
    }

    private static void ValidateTensor(double[,,]? tensor, bool allowNegativeInfinity)
    {
        if (tensor is null)
        {
            return;
        }

        if (tensor.GetLength(0) <= 0 || tensor.GetLength(1) <= 0 || tensor.GetLength(2) <= 0)
        {
            throw new ArgumentException("Tensor dimensions must be greater than zero.");
        }

        for (int ch = 0; ch < tensor.GetLength(0); ch++)
        {
            for (int mel = 0; mel < tensor.GetLength(1); mel++)
            {
                for (int frame = 0; frame < tensor.GetLength(2); frame++)
                {
                    double value = tensor[ch, mel, frame];
                    if (double.IsNaN(value))
                    {
                        throw new ArgumentOutOfRangeException(nameof(tensor), "Tensor must not contain NaN.");
                    }

                    if (double.IsInfinity(value))
                    {
                        bool isAllowedNegativeInfinity = allowNegativeInfinity && double.IsNegativeInfinity(value);
                        if (!isAllowedNegativeInfinity)
                        {
                            throw new ArgumentOutOfRangeException(nameof(tensor), "Tensor contains unsupported infinity.");
                        }
                    }
                }
            }
        }
    }
}
#pragma warning restore S2368
#pragma warning restore CA1819
#pragma warning restore CA1814
