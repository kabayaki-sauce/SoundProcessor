#pragma warning disable CA1814
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Domain.Models;
using MelSpectrogramAnalyzer.Core.Infrastructure.Analysis;

namespace MelSpectrogramAnalyzer.Core.Application.UseCases;

public sealed class MelSpectrogramInferenceWaveformFeatureUseCase
{
    private readonly MelSpectrogramInferenceFeatureExtractor featureExtractor;

    public MelSpectrogramInferenceWaveformFeatureUseCase()
        : this(new MelSpectrogramInferenceFeatureExtractor())
    {
    }

    internal MelSpectrogramInferenceWaveformFeatureUseCase(MelSpectrogramInferenceFeatureExtractor featureExtractor)
    {
        this.featureExtractor = featureExtractor ?? throw new ArgumentNullException(nameof(featureExtractor));
    }

    public Task<MelSpectrogramInferenceFeatureSummary> ExecuteAsync(
        MelSpectrogramInferenceWaveformRequest request,
        IMelSpectrogramInferenceNowTensorPointWriter? nowTensorPointWriter,
        IMelSpectrogramInferenceFramePointWriter? framePointWriter,
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

        MelSpectrogramInferenceNowTensorPoint[] points = new MelSpectrogramInferenceNowTensorPoint[request.NowMsList.Count];
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
            MelSpectrogramInferenceNowTensorPoint point = points[i];

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

        return Task.FromResult(new MelSpectrogramInferenceFeatureSummary(nowPointCount, framePointCount));
    }

    private static MelSpectrogramInferenceFramePoint ToFramePoint(
        MelSpectrogramInferenceNowTensorPoint point,
        int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, point.FrameCount);

        double[,]? linear = null;
        double[,]? db = null;

        if (point.Linear is not null)
        {
            linear = new double[point.Channels, point.MelBins];
            for (int channel = 0; channel < point.Channels; channel++)
            {
                for (int mel = 0; mel < point.MelBins; mel++)
                {
                    linear[channel, mel] = point.Linear[channel, mel, frameIndex];
                }
            }
        }

        if (point.Db is not null)
        {
            db = new double[point.Channels, point.MelBins];
            for (int channel = 0; channel < point.Channels; channel++)
            {
                for (int mel = 0; mel < point.MelBins; mel++)
                {
                    db[channel, mel] = point.Db[channel, mel, frameIndex];
                }
            }
        }

        return new MelSpectrogramInferenceFramePoint(
            point.Name,
            point.NowMs,
            frameIndex,
            linear,
            db);
    }
}
#pragma warning restore CA1814
