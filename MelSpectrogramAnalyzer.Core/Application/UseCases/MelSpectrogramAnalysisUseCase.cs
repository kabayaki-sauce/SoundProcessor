using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using MelSpectrogramAnalyzer.Core.Application.Errors;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Domain.Models;
using MelSpectrogramAnalyzer.Core.Infrastructure.Analysis;

namespace MelSpectrogramAnalyzer.Core.Application.UseCases;

public sealed class MelSpectrogramAnalysisUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;

    public MelSpectrogramAnalysisUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
    }

    public async Task<MelSpectrogramAnalysisSummary> ExecuteAsync(
        MelSpectrogramAnalysisRequest request,
        IMelSpectrogramAnalysisPointWriter pointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pointWriter);

        if (!File.Exists(request.InputFilePath))
        {
            throw new MelSpectrogramAnalysisException(MelSpectrogramAnalysisErrorCode.InputFileNotFound, request.InputFilePath);
        }

        if (request.WindowSamples <= 0)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidWindowSize,
                request.WindowSamples.ToString(CultureInfo.InvariantCulture));
        }

        if (request.HopSamples <= 0)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidHop,
                request.HopSamples.ToString(CultureInfo.InvariantCulture));
        }

        if (request.AnalysisSampleRate <= 0)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidTargetSampling,
                request.AnalysisSampleRate.ToString(CultureInfo.InvariantCulture));
        }

        if (request.MelBinCount <= 0)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidMelBinCount,
                request.MelBinCount.ToString(CultureInfo.InvariantCulture));
        }

        if (request.MelFminHz < 0
            || request.MelFmaxHz <= request.MelFminHz
            || request.MelFmaxHz > request.AnalysisSampleRate / 2.0)
        {
            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"fmin={request.MelFminHz}, fmax={request.MelFmaxHz}, nyquist={request.AnalysisSampleRate / 2.0}");
            throw new MelSpectrogramAnalysisException(MelSpectrogramAnalysisErrorCode.InvalidMelFrequencies, detail);
        }

        if (!Enum.IsDefined(request.MelScaleKind))
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidMelScale,
                request.MelScaleKind.ToString());
        }

        if (request.MelPower is not 1 and not 2)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidMelPower,
                request.MelPower.ToString(CultureInfo.InvariantCulture));
        }

        if (request.ProcessingThreads <= 0)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidMelBinCount,
                request.ProcessingThreads.ToString(CultureInfo.InvariantCulture));
        }

        if (double.IsNaN(request.MinLimitDb) || double.IsInfinity(request.MinLimitDb))
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidMinLimitDb,
                request.MinLimitDb.ToString(CultureInfo.InvariantCulture));
        }

        if (request.WindowSamples > int.MaxValue - 2)
        {
            throw new MelSpectrogramAnalysisException(
                MelSpectrogramAnalysisErrorCode.InvalidWindowSize,
                request.WindowSamples.ToString(CultureInfo.InvariantCulture));
        }

        int maxMelBinCount = ComputeMaxMelBinCount((int)request.WindowSamples);
        if (request.MelBinCount > maxMelBinCount)
        {
            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"melBinCount={request.MelBinCount}, max={maxMelBinCount}");
            throw new MelSpectrogramAnalysisException(MelSpectrogramAnalysisErrorCode.InvalidMelBinCount, detail);
        }

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
            .ConfigureAwait(false);

        MelSpectrogramWindowAnalyzer analyzer = new(
            request.AnalysisSampleRate,
            streamInfo.Channels,
            request.Name,
            request.WindowSamples,
            request.HopSamples,
            request.WindowPersistedValue,
            request.AnchorUnit,
            request.MelBinCount,
            request.MelFminHz,
            request.MelFmaxHz,
            request.MelScaleKind,
            request.MelPower,
            request.MinLimitDb,
            pointWriter,
            request.ProcessingThreads);

        int? targetSampleRateHz = request.AnalysisSampleRate == streamInfo.SampleRate
            ? null
            : request.AnalysisSampleRate;

        await pcmFrameReader
            .ReadFramesAsync(
                toolPaths,
                request.InputFilePath,
                streamInfo.Channels,
                analyzer,
                cancellationToken,
                targetSampleRateHz)
            .ConfigureAwait(false);

        return analyzer.BuildSummary();
    }

    private static int ComputeMaxMelBinCount(int windowSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSamples);

        int fftLength = 1;
        while (fftLength < windowSamples)
        {
            if (fftLength > int.MaxValue / 2)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSamples), "FFT size is too large.");
            }

            fftLength <<= 1;
        }

        return (fftLength / 2) + 1;
    }
}

