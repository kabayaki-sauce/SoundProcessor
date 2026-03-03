using System.Threading.Channels;
using AudioProcessor.Application.Ports;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Infrastructure.Analysis;

internal sealed class MelSpectrogramWindowAnalyzer : IAudioPcmFrameSink
{
    private readonly int sampleRate;
    private readonly int channels;
    private readonly string name;
    private readonly long windowSamples;
    private readonly long hopSamples;
    private readonly long windowPersistedValue;
    private readonly MelSpectrogramAnchorUnit anchorUnit;
    private readonly int melBinCount;
    private readonly double minLimitDb;
    private readonly IMelSpectrogramAnalysisPointWriter pointWriter;
    private readonly int processingThreads;
    private readonly bool useWindowPipelineMode;
    private readonly int melPower;
    private readonly double melDbFactor;

    private readonly int windowFrameCount;
    private readonly int ringLength;
    private readonly int fftLength;
    private readonly int positiveFrequencyBinCount;
    private readonly double[][] channelRingBuffers;
    private readonly double[] hannWindow;
    private readonly double[][] melFilterWeights;

    private readonly Channel<AnchorWorkItem>? anchorChannel;
    private readonly Task[] pipelineWorkers;

    private long frameIndex;
    private long pointCount;
    private long lastAnchor;
    private long nextAnchorSample;
    private Exception? pipelineException;

    public MelSpectrogramWindowAnalyzer(
        int sampleRate,
        int channels,
        string name,
        long windowSamples,
        long hopSamples,
        long windowPersistedValue,
        MelSpectrogramAnchorUnit anchorUnit,
        int melBinCount,
        double melFminHz,
        double melFmaxHz,
        MelSpectrogramScaleKind melScaleKind,
        int melPower,
        double minLimitDb,
        IMelSpectrogramAnalysisPointWriter pointWriter,
        int processingThreads)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowPersistedValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melBinCount);
        ArgumentOutOfRangeException.ThrowIfNegative(melFminHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melFmaxHz);
        ArgumentNullException.ThrowIfNull(pointWriter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingThreads);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melPower);

        if (!Enum.IsDefined(melScaleKind))
        {
            throw new ArgumentOutOfRangeException(nameof(melScaleKind));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(melFmaxHz, melFminHz);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(melFmaxHz, sampleRate / 2.0);

        if (melPower is not 1 and not 2)
        {
            throw new ArgumentOutOfRangeException(nameof(melPower));
        }

        this.sampleRate = sampleRate;
        this.channels = channels;
        this.name = name;
        this.windowSamples = windowSamples;
        this.hopSamples = hopSamples;
        this.windowPersistedValue = windowPersistedValue;
        this.anchorUnit = anchorUnit;
        this.melBinCount = melBinCount;
        this.minLimitDb = minLimitDb;
        this.pointWriter = pointWriter;
        this.processingThreads = processingThreads;
        this.melPower = melPower;
        melDbFactor = melPower == 1 ? 20.0 : 10.0;
        useWindowPipelineMode = processingThreads > channels;

        if (windowSamples > int.MaxValue - 2)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSamples), "Window size is too large.");
        }

        windowFrameCount = checked((int)windowSamples);
        ringLength = checked(windowFrameCount + 2);

        channelRingBuffers = new double[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            channelRingBuffers[channel] = new double[ringLength];
        }

        hannWindow = BuildHannWindow(windowFrameCount);
        fftLength = NextPowerOfTwo(windowFrameCount);
        positiveFrequencyBinCount = (fftLength / 2) + 1;

        if (melBinCount > positiveFrequencyBinCount)
        {
            throw new ArgumentOutOfRangeException(nameof(melBinCount), "Mel bin count exceeds positive frequency bins.");
        }

        melFilterWeights = BuildMelFilterWeights(
            sampleRate,
            fftLength,
            positiveFrequencyBinCount,
            melBinCount,
            melFminHz,
            melFmaxHz,
            melScaleKind);

        nextAnchorSample = hopSamples;

        if (useWindowPipelineMode)
        {
            int channelCapacity = Math.Max(1, processingThreads * 2);
            anchorChannel = Channel.CreateBounded<AnchorWorkItem>(
                new BoundedChannelOptions(channelCapacity)
                {
                    SingleWriter = true,
                    SingleReader = false,
                    FullMode = BoundedChannelFullMode.Wait,
                });

            pipelineWorkers = new Task[processingThreads];
            for (int i = 0; i < pipelineWorkers.Length; i++)
            {
                pipelineWorkers[i] = Task.Run(RunPipelineWorkerAsync, CancellationToken.None);
            }
        }
        else
        {
            pipelineWorkers = Array.Empty<Task>();
        }
    }

    public void OnFrame(ReadOnlySpan<float> frameSamples)
    {
        if (frameSamples.Length != channels)
        {
            throw new ArgumentException("Unexpected channel count.", nameof(frameSamples));
        }

        ThrowIfPipelineFaulted();

        int writeIndex = checked((int)(frameIndex % ringLength));
        for (int channel = 0; channel < channels; channel++)
        {
            channelRingBuffers[channel][writeIndex] = frameSamples[channel];
        }

        frameIndex++;

        while (nextAnchorSample <= frameIndex)
        {
            EmitPoint(nextAnchorSample);
            nextAnchorSample += hopSamples;
        }
    }

    public MelSpectrogramAnalysisSummary BuildSummary()
    {
        if (useWindowPipelineMode)
        {
            anchorChannel!.Writer.TryComplete();

            try
            {
                Task.WaitAll(pipelineWorkers);
            }
            catch (AggregateException aggregateException)
            {
                Exception baseException = aggregateException.GetBaseException();
                pipelineException ??= baseException;
            }

            ThrowIfPipelineFaulted();
        }

        return new MelSpectrogramAnalysisSummary(checked((int)pointCount), frameIndex, lastAnchor);
    }

    private void EmitPoint(long anchorSample)
    {
        long anchorValue = anchorUnit == MelSpectrogramAnchorUnit.Sample
            ? anchorSample
            : FrameToElapsedMsFloor(anchorSample, sampleRate);
        lastAnchor = anchorValue;

        if (useWindowPipelineMode)
        {
            EnqueueAnchorWorkItem(anchorSample, anchorValue);
            return;
        }

        EmitImmediate(anchorSample, anchorValue);
    }

    private void EnqueueAnchorWorkItem(long anchorSample, long anchorValue)
    {
        long startFrame = anchorSample - windowSamples;
        double[][] capturedWindows = CaptureChannelWindows(startFrame);
        AnchorWorkItem workItem = new(anchorValue, capturedWindows);

        try
        {
            anchorChannel!.Writer.WriteAsync(workItem).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            ThrowIfPipelineFaulted();
            throw;
        }
    }

    private void EmitImmediate(long anchorSample, long anchorValue)
    {
        long startFrame = anchorSample - windowSamples;
        double[][] binsByChannel = new double[channels][];

        if (processingThreads > 1 && channels > 1)
        {
            Parallel.For(
                0,
                channels,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = processingThreads,
                },
                channel =>
                {
                    binsByChannel[channel] = AnalyzeChannelFromRing(channel, startFrame);
                });
        }
        else
        {
            for (int channel = 0; channel < channels; channel++)
            {
                binsByChannel[channel] = AnalyzeChannelFromRing(channel, startFrame);
            }
        }

        for (int channel = 0; channel < channels; channel++)
        {
            MelSpectrogramAnalysisPoint point = new(name, channel, windowPersistedValue, anchorValue, binsByChannel[channel]);
            pointWriter.Write(point);
            pointCount++;
        }
    }

    private async Task RunPipelineWorkerAsync()
    {
        ArgumentNullException.ThrowIfNull(anchorChannel);

        double[] fftReal = new double[fftLength];
        double[] fftImag = new double[fftLength];

        await foreach (AnchorWorkItem workItem in anchorChannel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            for (int channel = 0; channel < channels; channel++)
            {
                LoadWindowFromCaptured(workItem.ChannelWindows[channel], fftReal, fftImag);
                FftMath.TransformInPlace(fftReal, fftImag);
                double[] bins = BuildBandValues(fftReal, fftImag);

                MelSpectrogramAnalysisPoint point = new(name, channel, windowPersistedValue, workItem.AnchorValue, bins);
                pointWriter.Write(point);
            }

            _ = Interlocked.Add(ref pointCount, channels);
        }
    }

    private double[][] CaptureChannelWindows(long startFrame)
    {
        double[][] capturedWindows = new double[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            double[] captured = new double[windowFrameCount];
            for (int offset = 0; offset < windowFrameCount; offset++)
            {
                long sourceFrame = startFrame + offset;
                captured[offset] = sourceFrame < 0 ? 0 : ReadBufferedSample(channel, sourceFrame);
            }

            capturedWindows[channel] = captured;
        }

        return capturedWindows;
    }

    private double[] AnalyzeChannelFromRing(int channel, long startFrame)
    {
        double[] fftReal = new double[fftLength];
        double[] fftImag = new double[fftLength];

        LoadWindowFromRing(channel, startFrame, fftReal, fftImag);
        FftMath.TransformInPlace(fftReal, fftImag);
        return BuildBandValues(fftReal, fftImag);
    }

    private void LoadWindowFromRing(int channel, long startFrame, double[] fftReal, double[] fftImag)
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

    private void LoadWindowFromCaptured(double[] captured, double[] fftReal, double[] fftImag)
    {
        Array.Clear(fftReal, 0, fftReal.Length);
        Array.Clear(fftImag, 0, fftImag.Length);

        for (int offset = 0; offset < windowFrameCount; offset++)
        {
            fftReal[offset] = captured[offset] * hannWindow[offset];
        }
    }

    private double[] BuildBandValues(double[] fftReal, double[] fftImag)
    {
        double[] linearSpectrum = new double[positiveFrequencyBinCount];
        for (int frequency = 0; frequency < positiveFrequencyBinCount; frequency++)
        {
            double real = fftReal[frequency];
            double imag = fftImag[frequency];
            double magnitudeSquared = (real * real) + (imag * imag);
            linearSpectrum[frequency] = melPower == 1
                ? Math.Sqrt(magnitudeSquared)
                : magnitudeSquared;
        }

        double[] melBins = new double[melBinCount];
        for (int melBand = 0; melBand < melBinCount; melBand++)
        {
            double[] weights = melFilterWeights[melBand];
            double melValue = 0;

            for (int frequency = 0; frequency < positiveFrequencyBinCount; frequency++)
            {
                double weight = weights[frequency];
                if (weight <= 0)
                {
                    continue;
                }

                melValue += weight * linearSpectrum[frequency];
            }

            double db = melValue <= 0 ? double.NegativeInfinity : melDbFactor * Math.Log10(melValue);
            if (db < minLimitDb)
            {
                db = minLimitDb;
            }

            melBins[melBand] = db;
        }

        return melBins;
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

    private void ThrowIfPipelineFaulted()
    {
        if (pipelineException is not null)
        {
            throw new InvalidOperationException("Window pipeline failed.", pipelineException);
        }
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

    private static double[][] BuildMelFilterWeights(
        int sampleRate,
        int fftLength,
        int positiveBinCount,
        int melBinCount,
        double melFminHz,
        double melFmaxHz,
        MelSpectrogramScaleKind melScaleKind)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(positiveBinCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melBinCount);
        ArgumentOutOfRangeException.ThrowIfNegative(melFminHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(melFmaxHz);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(melFmaxHz, melFminHz);

        double melStart = HzToMel(melFminHz, melScaleKind);
        double melEnd = HzToMel(melFmaxHz, melScaleKind);

        double[] melPoints = new double[melBinCount + 2];
        double melStep = (melEnd - melStart) / (melPoints.Length - 1);
        for (int i = 0; i < melPoints.Length; i++)
        {
            melPoints[i] = melStart + (melStep * i);
        }

        double[] hzPoints = new double[melPoints.Length];
        for (int i = 0; i < melPoints.Length; i++)
        {
            hzPoints[i] = MelToHz(melPoints[i], melScaleKind);
        }

        double[] binFrequencies = new double[positiveBinCount];
        for (int i = 0; i < positiveBinCount; i++)
        {
            binFrequencies[i] = i * sampleRate / (double)fftLength;
        }

        double[][] filterWeights = new double[melBinCount][];
        for (int melBand = 0; melBand < melBinCount; melBand++)
        {
            double leftHz = hzPoints[melBand];
            double centerHz = hzPoints[melBand + 1];
            double rightHz = hzPoints[melBand + 2];

            if (centerHz <= leftHz || rightHz <= centerHz)
            {
                throw new ArgumentOutOfRangeException(nameof(melBinCount), "Invalid mel frequency boundaries.");
            }

            double[] weights = new double[positiveBinCount];
            for (int i = 0; i < positiveBinCount; i++)
            {
                double frequency = binFrequencies[i];
                double rising = (frequency - leftHz) / (centerHz - leftHz);
                double falling = (rightHz - frequency) / (rightHz - centerHz);
                double weight = Math.Max(0, Math.Min(rising, falling));

                if (melScaleKind == MelSpectrogramScaleKind.Slaney)
                {
                    weight *= 2.0 / (rightHz - leftHz);
                }

                weights[i] = weight;
            }

            filterWeights[melBand] = weights;
        }

        return filterWeights;
    }

    private static double HzToMel(double frequencyHz, MelSpectrogramScaleKind melScaleKind)
    {
        return melScaleKind switch
        {
            MelSpectrogramScaleKind.Slaney => HzToMelSlaney(frequencyHz),
            MelSpectrogramScaleKind.Htk => 2595.0 * Math.Log10(1.0 + (frequencyHz / 700.0)),
            _ => throw new ArgumentOutOfRangeException(nameof(melScaleKind)),
        };
    }

    private static double MelToHz(double mel, MelSpectrogramScaleKind melScaleKind)
    {
        return melScaleKind switch
        {
            MelSpectrogramScaleKind.Slaney => MelToHzSlaney(mel),
            MelSpectrogramScaleKind.Htk => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(melScaleKind)),
        };
    }

    private static double HzToMelSlaney(double frequencyHz)
    {
        const double fMin = 0.0;
        const double fSp = 200.0 / 3.0;
        const double minLogHz = 1000.0;
        const double minLogMel = (minLogHz - fMin) / fSp;
        double logStep = Math.Log(6.4) / 27.0;

        if (frequencyHz < minLogHz)
        {
            return (frequencyHz - fMin) / fSp;
        }

        return minLogMel + (Math.Log(frequencyHz / minLogHz) / logStep);
    }

    private static double MelToHzSlaney(double mel)
    {
        const double fMin = 0.0;
        const double fSp = 200.0 / 3.0;
        const double minLogHz = 1000.0;
        const double minLogMel = (minLogHz - fMin) / fSp;
        double logStep = Math.Log(6.4) / 27.0;

        if (mel < minLogMel)
        {
            return fMin + (fSp * mel);
        }

        return minLogHz * Math.Exp(logStep * (mel - minLogMel));
    }

    private static long FrameToElapsedMsFloor(long frameCount, int sampleRate)
    {
        return checked(frameCount * 1000 / sampleRate);
    }

    private readonly record struct AnchorWorkItem(long AnchorValue, double[][] ChannelWindows);

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

