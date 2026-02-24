using AudioProcessor.Application.Ports;
using SFFTAnalyzer.Core.Application.Ports;
using SFFTAnalyzer.Core.Domain.Models;

namespace SFFTAnalyzer.Core.Infrastructure.Analysis;

internal sealed class SfftWindowAnalyzer : IAudioPcmFrameSink
{
    private readonly int sampleRate;
    private readonly int channels;
    private readonly string name;
    private readonly long windowMs;
    private readonly long hopMs;
    private readonly int binCount;
    private readonly double minLimitDb;
    private readonly ISfftAnalysisPointWriter pointWriter;

    private readonly long windowFrames;
    private readonly int windowFrameCount;
    private readonly int ringLength;
    private readonly double[][] channelRingBuffers;
    private readonly double[] hannWindow;
    private readonly int positiveFrequencyBinCount;

    private readonly double[] fftReal;
    private readonly double[] fftImag;

    private long frameIndex;
    private int pointCount;
    private long lastMs;
    private long nextAnchorMs;

    public SfftWindowAnalyzer(
        int sampleRate,
        int channels,
        string name,
        long windowMs,
        long hopMs,
        int binCount,
        double minLimitDb,
        ISfftAnalysisPointWriter pointWriter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binCount);
        ArgumentNullException.ThrowIfNull(pointWriter);

        this.sampleRate = sampleRate;
        this.channels = channels;
        this.name = name;
        this.windowMs = windowMs;
        this.hopMs = hopMs;
        this.binCount = binCount;
        this.minLimitDb = minLimitDb;
        this.pointWriter = pointWriter;

        long calculatedWindowFrames = TimeMsToFrameFloor(windowMs, sampleRate);
        this.windowFrames = Math.Max(1, calculatedWindowFrames);

        if (this.windowFrames > int.MaxValue - 2)
        {
            throw new ArgumentOutOfRangeException(nameof(windowMs), "Window size is too large.");
        }

        windowFrameCount = checked((int)this.windowFrames);
        ringLength = checked(windowFrameCount + 2);

        channelRingBuffers = new double[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            channelRingBuffers[channel] = new double[ringLength];
        }

        hannWindow = BuildHannWindow(windowFrameCount);
        int fftLength = NextPowerOfTwo(windowFrameCount);
        positiveFrequencyBinCount = (fftLength / 2) + 1;

        fftReal = new double[fftLength];
        fftImag = new double[fftLength];

        nextAnchorMs = hopMs;
    }

    public void OnFrame(ReadOnlySpan<float> frameSamples)
    {
        if (frameSamples.Length != channels)
        {
            throw new ArgumentException("Unexpected channel count.", nameof(frameSamples));
        }

        int writeIndex = checked((int)(frameIndex % ringLength));
        for (int channel = 0; channel < channels; channel++)
        {
            channelRingBuffers[channel][writeIndex] = frameSamples[channel];
        }

        frameIndex++;

        long elapsedMs = FrameToElapsedMsFloor(frameIndex, sampleRate);
        while (nextAnchorMs <= elapsedMs)
        {
            EmitPoint(nextAnchorMs);
            nextAnchorMs += hopMs;
        }
    }

    public SfftAnalysisSummary BuildSummary()
    {
        return new SfftAnalysisSummary(pointCount, frameIndex, lastMs);
    }

    private void EmitPoint(long anchorMs)
    {
        long endFrameExclusive = TimeMsToFrameFloor(anchorMs, sampleRate);
        long startFrame = endFrameExclusive - windowFrames;

        for (int channel = 0; channel < channels; channel++)
        {
            LoadWindow(channel, startFrame);
            FftMath.TransformInPlace(fftReal, fftImag);

            double[] bins = BuildBandValues();
            SfftAnalysisPoint point = new(name, channel, windowMs, anchorMs, bins);
            pointWriter.Write(point);
            pointCount++;
        }

        lastMs = anchorMs;
    }

    private void LoadWindow(int channel, long startFrame)
    {
        Array.Clear(fftReal, 0, fftReal.Length);
        Array.Clear(fftImag, 0, fftImag.Length);

        for (int offset = 0; offset < windowFrameCount; offset++)
        {
            long sourceFrame = startFrame + offset;
            double sample = sourceFrame < 0 ? 0 : ReadBufferedSample(channel, sourceFrame);
            fftReal[offset] = sample * hannWindow[offset];
        }
    }

    private double[] BuildBandValues()
    {
        double[] bins = new double[binCount];

        for (int band = 0; band < binCount; band++)
        {
            int bandStart = checked((int)((long)band * positiveFrequencyBinCount / binCount));
            int bandEnd = checked((int)((long)(band + 1) * positiveFrequencyBinCount / binCount));

            if (bandEnd <= bandStart)
            {
                bandEnd = Math.Min(positiveFrequencyBinCount, bandStart + 1);
            }

            if (band == binCount - 1)
            {
                bandEnd = positiveFrequencyBinCount;
            }

            double sumMagnitude = 0;
            int count = 0;

            for (int frequency = bandStart; frequency < bandEnd; frequency++)
            {
                double real = fftReal[frequency];
                double imag = fftImag[frequency];
                double magnitude = Math.Sqrt((real * real) + (imag * imag));
                sumMagnitude += magnitude;
                count++;
            }

            double averageMagnitude = count == 0 ? 0 : sumMagnitude / count;
            double db = averageMagnitude <= 0 ? double.NegativeInfinity : 20 * Math.Log10(averageMagnitude);
            if (db < minLimitDb)
            {
                db = minLimitDb;
            }

            bins[band] = db;
        }

        return bins;
    }

    private double ReadBufferedSample(int channel, long sourceFrame)
    {
        if (sourceFrame < 0)
        {
            return 0;
        }

        if (sourceFrame >= frameIndex)
        {
            return 0;
        }

        long oldestRetainedFrame = Math.Max(0, frameIndex - ringLength);
        if (sourceFrame < oldestRetainedFrame)
        {
            throw new InvalidOperationException("Insufficient buffer for requested frame window.");
        }

        int index = checked((int)(sourceFrame % ringLength));
        return channelRingBuffers[channel][index];
    }

    private static double[] BuildHannWindow(int sampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);

        if (sampleCount == 1)
        {
            return [1.0];
        }

        double[] window = new double[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (sampleCount - 1)));
        }

        return window;
    }

    private static int NextPowerOfTwo(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

        int result = 1;
        while (result < value)
        {
            if (result > int.MaxValue / 2)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "FFT size is too large.");
            }

            result <<= 1;
        }

        return result;
    }

    private static long FrameToElapsedMsFloor(long frameCount, int sampleRate)
    {
        return checked(frameCount * 1000 / sampleRate);
    }

    private static long TimeMsToFrameFloor(long ms, int sampleRate)
    {
        return checked(ms * sampleRate / 1000);
    }

    private static class FftMath
    {
        public static void TransformInPlace(double[] real, double[] imag)
        {
            ArgumentNullException.ThrowIfNull(real);
            ArgumentNullException.ThrowIfNull(imag);

            if (real.Length != imag.Length)
            {
                throw new ArgumentException("Real and imaginary arrays must have same length.");
            }

            int length = real.Length;
            if (length == 0 || (length & (length - 1)) != 0)
            {
                throw new ArgumentException("FFT length must be a power of two.");
            }

            BitReverse(real, imag);

            for (int blockSize = 2; blockSize <= length; blockSize <<= 1)
            {
                int halfSize = blockSize / 2;
                double theta = -2 * Math.PI / blockSize;
                double phaseStepReal = Math.Cos(theta);
                double phaseStepImag = Math.Sin(theta);

                for (int start = 0; start < length; start += blockSize)
                {
                    double phaseReal = 1;
                    double phaseImag = 0;

                    for (int offset = 0; offset < halfSize; offset++)
                    {
                        int evenIndex = start + offset;
                        int oddIndex = evenIndex + halfSize;

                        double oddReal = (real[oddIndex] * phaseReal) - (imag[oddIndex] * phaseImag);
                        double oddImag = (real[oddIndex] * phaseImag) + (imag[oddIndex] * phaseReal);

                        real[oddIndex] = real[evenIndex] - oddReal;
                        imag[oddIndex] = imag[evenIndex] - oddImag;
                        real[evenIndex] += oddReal;
                        imag[evenIndex] += oddImag;

                        double nextPhaseReal = (phaseReal * phaseStepReal) - (phaseImag * phaseStepImag);
                        double nextPhaseImag = (phaseReal * phaseStepImag) + (phaseImag * phaseStepReal);
                        phaseReal = nextPhaseReal;
                        phaseImag = nextPhaseImag;
                    }
                }
            }
        }

        private static void BitReverse(double[] real, double[] imag)
        {
            int n = real.Length;
            int j = 0;

            for (int i = 1; i < n; i++)
            {
                int bit = n >> 1;
                while ((j & bit) != 0)
                {
                    j ^= bit;
                    bit >>= 1;
                }

                j ^= bit;

                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }
            }
        }
    }
}
