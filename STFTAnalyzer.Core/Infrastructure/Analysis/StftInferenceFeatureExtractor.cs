#pragma warning disable CA1814
#pragma warning disable CA1822
#pragma warning disable S2325
#pragma warning disable IDE0031
#pragma warning disable IDE0047
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Infrastructure.Analysis;

internal sealed class StftInferenceFeatureExtractor
{
    public double[][] NormalizeWaveformToStereo(
        float[][] waveform,
        StftInferenceChannelHandlingMode channelHandlingMode)
    {
        ArgumentNullException.ThrowIfNull(waveform);
        if (!Enum.IsDefined(channelHandlingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(channelHandlingMode));
        }

        if (waveform.Length == 0)
        {
            throw new ArgumentException("Waveform must contain at least one channel.", nameof(waveform));
        }

        return channelHandlingMode switch
        {
            StftInferenceChannelHandlingMode.DuplicateMonoAndTakeFirstTwo => NormalizeDuplicateOrTakeFirstTwo(waveform),
            StftInferenceChannelHandlingMode.StrictTwoChannels => NormalizeStrictTwoChannels(waveform),
            _ => throw new ArgumentOutOfRangeException(nameof(channelHandlingMode)),
        };
    }

    public StftInferenceNowTensorPoint Extract(
        StftInferenceWaveformRequest request,
        double[][] stereoWaveform,
        int nowMs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stereoWaveform);
        cancellationToken.ThrowIfCancellationRequested();

        if (stereoWaveform.Length != 2)
        {
            throw new ArgumentException("Stereo waveform (2 channels) is required.", nameof(stereoWaveform));
        }

        double[][] segment = ExtractSegment(
            stereoWaveform,
            request.SampleRate,
            nowMs,
            request.SegmentDurationSeconds);
        double[] window = BuildPaddedHannWindow(request.NFft, request.WinLength);

        double[][] analysisTarget = request.Center
            ? ApplyCenterPadding(segment, request.NFft, request.PadMode)
            : segment;

        int frequencyBins = (request.NFft / 2) + 1;
        int frameCount = ComputeFrameCount(analysisTarget[0].Length, request.NFft, request.HopLength);

        double[,,]? linear = request.EmitLinear
            ? new double[2, frequencyBins, frameCount]
            : null;
        double[,,]? db = request.EmitDb
            ? new double[2, frequencyBins, frameCount]
            : null;

        double? sanitizeMinDbfs = request.SanitizeMinDbfs;
        double dbFactor = 20.0 / request.Power;
        double linearFloor = sanitizeMinDbfs.HasValue
            ? Math.Pow(10.0, (sanitizeMinDbfs.Value / 20.0) * request.Power)
            : 0;

        Complex[] spectrum = new Complex[request.NFft];
        for (int channel = 0; channel < 2; channel++)
        {
            double[] source = analysisTarget[channel];
            for (int frame = 0; frame < frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int start = frame * request.HopLength;
                for (int i = 0; i < request.NFft; i++)
                {
                    int sourceIndex = start + i;
                    double sample = sourceIndex < source.Length ? source[sourceIndex] : 0;
                    spectrum[i] = new Complex(sample * window[i], 0);
                }

                Fourier.Forward(spectrum, FourierOptions.Matlab);
                for (int frequency = 0; frequency < frequencyBins; frequency++)
                {
                    double magnitude = spectrum[frequency].Magnitude;
                    double linearValue = Math.Pow(magnitude, request.Power);
                    double dbValue = linearValue <= 0
                        ? double.NegativeInfinity
                        : dbFactor * Math.Log10(linearValue);

                    if (sanitizeMinDbfs.HasValue)
                    {
                        linearValue = SanitizeLinear(linearValue, linearFloor);
                        dbValue = SanitizeDb(dbValue, sanitizeMinDbfs.Value);
                    }

                    if (linear is not null)
                    {
                        linear[channel, frequency, frame] = linearValue;
                    }

                    if (db is not null)
                    {
                        db[channel, frequency, frame] = dbValue;
                    }
                }
            }
        }

        return new StftInferenceNowTensorPoint(request.Name, nowMs, linear, db);
    }

    private static double SanitizeLinear(double value, double floor)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < floor)
        {
            return floor;
        }

        return value;
    }

    private static double SanitizeDb(double value, double minDbfs)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < minDbfs)
        {
            return minDbfs;
        }

        return value;
    }

    private static double[][] NormalizeDuplicateOrTakeFirstTwo(float[][] waveform)
    {
        if (waveform.Length == 1)
        {
            float[] channel = waveform[0] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
            return [ToDoubleArray(channel), ToDoubleArray(channel)];
        }

        float[] left = waveform[0] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
        float[] right = waveform[1] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Waveform channels must have same sample length.", nameof(waveform));
        }

        return [ToDoubleArray(left), ToDoubleArray(right)];
    }

    private static double[][] NormalizeStrictTwoChannels(float[][] waveform)
    {
        if (waveform.Length != 2)
        {
            throw new ArgumentException("StrictTwoChannels mode requires exactly two channels.", nameof(waveform));
        }

        float[] left = waveform[0] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
        float[] right = waveform[1] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Waveform channels must have same sample length.", nameof(waveform));
        }

        return [ToDoubleArray(left), ToDoubleArray(right)];
    }

    private static double[] ToDoubleArray(float[] source)
    {
        double[] converted = new double[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            converted[i] = source[i];
        }

        return converted;
    }

    private static double[][] ExtractSegment(
        double[][] waveform,
        int sampleRate,
        int nowMs,
        double durationSeconds)
    {
        int totalSamples = checked((int)(durationSeconds * sampleRate));
        if (totalSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Segment duration must produce positive sample length.");
        }

        int waveformLength = waveform[0].Length;
        int nowSample = checked((int)((nowMs / 1000.0) * sampleRate));
        int endSample = Math.Min(Math.Max(nowSample, 0), waveformLength);
        int startSample = endSample - totalSamples;

        int leftPad = Math.Max(0, -startSample);
        int validStart = Math.Max(0, startSample);
        int validEnd = endSample;
        int validWidth = Math.Max(validEnd - validStart, 0);

        double[][] segment = [new double[totalSamples], new double[totalSamples]];
        for (int channel = 0; channel < 2; channel++)
        {
            if (validWidth > 0)
            {
                Array.Copy(waveform[channel], validStart, segment[channel], leftPad, validWidth);
            }
        }

        return segment;
    }

    private static double[][] ApplyCenterPadding(
        double[][] segment,
        int nFft,
        StftInferencePadMode padMode)
    {
        int pad = nFft / 2;
        if (pad <= 0)
        {
            return segment;
        }

        return
        [
            Pad1D(segment[0], pad, padMode),
            Pad1D(segment[1], pad, padMode),
        ];
    }

    private static double[] Pad1D(double[] source, int pad, StftInferencePadMode padMode)
    {
        double[] padded = new double[source.Length + (pad * 2)];
        for (int i = 0; i < padded.Length; i++)
        {
            int sourceIndex = i - pad;
            if (sourceIndex >= 0 && sourceIndex < source.Length)
            {
                padded[i] = source[sourceIndex];
                continue;
            }

            padded[i] = padMode switch
            {
                StftInferencePadMode.ConstantZero => 0,
                StftInferencePadMode.Reflect => ResolveReflectSample(source, sourceIndex),
                _ => throw new ArgumentOutOfRangeException(nameof(padMode)),
            };
        }

        return padded;
    }

    private static double ResolveReflectSample(double[] source, int sourceIndex)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        if (source.Length == 1)
        {
            return source[0];
        }

        int reflected = ReflectIndex(sourceIndex, source.Length);
        return source[reflected];
    }

    private static int ReflectIndex(int index, int length)
    {
        int period = (2 * length) - 2;
        int mod = index % period;
        if (mod < 0)
        {
            mod += period;
        }

        return mod < length ? mod : period - mod;
    }

    private static int ComputeFrameCount(int signalLength, int nFft, int hopLength)
    {
        if (signalLength < nFft)
        {
            return 1;
        }

        return ((signalLength - nFft) / hopLength) + 1;
    }

    private static double[] BuildPaddedHannWindow(int nFft, int winLength)
    {
        double[] padded = new double[nFft];
        if (winLength == 1)
        {
            padded[(nFft - 1) / 2] = 1;
            return padded;
        }

        double[] baseWindow = new double[winLength];
        for (int i = 0; i < winLength; i++)
        {
            baseWindow[i] = 0.5 - (0.5 * Math.Cos((2 * Math.PI * i) / winLength));
        }

        int leftPad = (nFft - winLength) / 2;
        for (int i = 0; i < winLength; i++)
        {
            padded[leftPad + i] = baseWindow[i];
        }

        return padded;
    }
}
#pragma warning restore IDE0047
#pragma warning restore IDE0031
#pragma warning restore S2325
#pragma warning restore CA1822
#pragma warning restore CA1814
