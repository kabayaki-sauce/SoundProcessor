#pragma warning disable CA1814
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Domain.Models;
using STFTAnalyzer.Core.Infrastructure.Analysis;

namespace STFTAnalyzer.Core.Application.UseCases;

public sealed class StftInferenceWaveformFeatureUseCase
{
    private readonly StftInferenceFeatureExtractor featureExtractor;

    public StftInferenceWaveformFeatureUseCase()
        : this(new StftInferenceFeatureExtractor())
    {
    }

    internal StftInferenceWaveformFeatureUseCase(StftInferenceFeatureExtractor featureExtractor)
    {
        this.featureExtractor = featureExtractor ?? throw new ArgumentNullException(nameof(featureExtractor));
    }

    public Task<StftInferenceFeatureSummary> ExecuteAsync(
        StftInferenceWaveformRequest request,
        IStftInferenceNowTensorPointWriter? nowTensorPointWriter,
        IStftInferenceFramePointWriter? framePointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (nowTensorPointWriter is null && framePointWriter is null)
        {
            throw new ArgumentException("At least one writer must be provided.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        double[][] stereoWaveform = featureExtractor.NormalizeWaveformToStereo(
            request.Waveform,
            request.ChannelHandlingMode);

        StftInferenceNowTensorPoint[] points = new StftInferenceNowTensorPoint[request.NowMsList.Count];
        if (request.ProcessingThreads > 1 && points.Length > 1)
        {
            Parallel.For(
                0,
                points.Length,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = request.ProcessingThreads,
                    CancellationToken = cancellationToken,
                },
                index =>
                {
                    int nowMs = request.NowMsList[index];
                    points[index] = featureExtractor.Extract(request, stereoWaveform, nowMs, cancellationToken);
                });
        }
        else
        {
            for (int i = 0; i < points.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                points[i] = featureExtractor.Extract(request, stereoWaveform, request.NowMsList[i], cancellationToken);
            }
        }

        int nowPointCount = 0;
        int framePointCount = 0;
        for (int i = 0; i < points.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StftInferenceNowTensorPoint point = points[i];

            nowTensorPointWriter?.Write(point);
            nowPointCount++;

            if (framePointWriter is null)
            {
                continue;
            }

            for (int frame = 0; frame < point.FrameCount; frame++)
            {
                framePointWriter.Write(ToFramePoint(point, frame));
                framePointCount++;
            }
        }

        return Task.FromResult(new StftInferenceFeatureSummary(nowPointCount, framePointCount));
    }

    private static StftInferenceFramePoint ToFramePoint(StftInferenceNowTensorPoint point, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, point.FrameCount);

        double[,]? linear = null;
        double[,]? db = null;

        if (point.Linear is not null)
        {
            linear = new double[point.Channels, point.FrequencyBins];
            for (int channel = 0; channel < point.Channels; channel++)
            {
                for (int frequency = 0; frequency < point.FrequencyBins; frequency++)
                {
                    linear[channel, frequency] = point.Linear[channel, frequency, frameIndex];
                }
            }
        }

        if (point.Db is not null)
        {
            db = new double[point.Channels, point.FrequencyBins];
            for (int channel = 0; channel < point.Channels; channel++)
            {
                for (int frequency = 0; frequency < point.FrequencyBins; frequency++)
                {
                    db[channel, frequency] = point.Db[channel, frequency, frameIndex];
                }
            }
        }

        return new StftInferenceFramePoint(
            point.Name,
            point.NowMs,
            frameIndex,
            linear,
            db);
    }
}
#pragma warning restore CA1814
