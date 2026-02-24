using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using STFTAnalyzer.Core.Application.Errors;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Domain.Models;
using STFTAnalyzer.Core.Infrastructure.Analysis;

namespace STFTAnalyzer.Core.Application.UseCases;

public sealed class StftAnalysisUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;

    public StftAnalysisUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
    }

    public async Task<StftAnalysisSummary> ExecuteAsync(
        StftAnalysisRequest request,
        IStftAnalysisPointWriter pointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pointWriter);

        if (!File.Exists(request.InputFilePath))
        {
            throw new StftAnalysisException(StftAnalysisErrorCode.InputFileNotFound, request.InputFilePath);
        }

        if (request.WindowSamples <= 0)
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidWindowSize,
                request.WindowSamples.ToString(CultureInfo.InvariantCulture));
        }

        if (request.HopSamples <= 0)
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidHop,
                request.HopSamples.ToString(CultureInfo.InvariantCulture));
        }

        if (request.AnalysisSampleRate <= 0)
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidTargetSampling,
                request.AnalysisSampleRate.ToString(CultureInfo.InvariantCulture));
        }

        if (request.BinCount <= 0)
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidBinCount,
                request.BinCount.ToString(CultureInfo.InvariantCulture));
        }

        if (double.IsNaN(request.MinLimitDb) || double.IsInfinity(request.MinLimitDb))
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidMinLimitDb,
                request.MinLimitDb.ToString(CultureInfo.InvariantCulture));
        }

        if (request.WindowSamples > int.MaxValue - 2)
        {
            throw new StftAnalysisException(
                StftAnalysisErrorCode.InvalidWindowSize,
                request.WindowSamples.ToString(CultureInfo.InvariantCulture));
        }

        int maxBinCount = ComputeMaxBinCount((int)request.WindowSamples);
        if (request.BinCount > maxBinCount)
        {
            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"binCount={request.BinCount}, max={maxBinCount}");
            throw new StftAnalysisException(StftAnalysisErrorCode.InvalidBinCount, detail);
        }

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
            .ConfigureAwait(false);

        StftWindowAnalyzer analyzer = new(
            request.AnalysisSampleRate,
            streamInfo.Channels,
            request.Name,
            request.WindowSamples,
            request.HopSamples,
            request.WindowPersistedValue,
            request.AnchorUnit,
            request.BinCount,
            request.MinLimitDb,
            pointWriter);

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

    private static int ComputeMaxBinCount(int windowSamples)
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
