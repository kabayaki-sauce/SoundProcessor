using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using SFFTAnalyzer.Core.Application.Errors;
using SFFTAnalyzer.Core.Application.Models;
using SFFTAnalyzer.Core.Application.Ports;
using SFFTAnalyzer.Core.Domain.Models;
using SFFTAnalyzer.Core.Infrastructure.Analysis;

namespace SFFTAnalyzer.Core.Application.UseCases;

public sealed class SfftAnalysisUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;

    public SfftAnalysisUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
    }

    public async Task<SfftAnalysisSummary> ExecuteAsync(
        SfftAnalysisRequest request,
        ISfftAnalysisPointWriter pointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pointWriter);

        if (!File.Exists(request.InputFilePath))
        {
            throw new SfftAnalysisException(SfftAnalysisErrorCode.InputFileNotFound, request.InputFilePath);
        }

        if (request.WindowSizeMs <= 0)
        {
            throw new SfftAnalysisException(
                SfftAnalysisErrorCode.InvalidWindowSize,
                request.WindowSizeMs.ToString(CultureInfo.InvariantCulture));
        }

        if (request.HopMs <= 0)
        {
            throw new SfftAnalysisException(
                SfftAnalysisErrorCode.InvalidHop,
                request.HopMs.ToString(CultureInfo.InvariantCulture));
        }

        if (request.BinCount <= 0)
        {
            throw new SfftAnalysisException(
                SfftAnalysisErrorCode.InvalidBinCount,
                request.BinCount.ToString(CultureInfo.InvariantCulture));
        }

        if (double.IsNaN(request.MinLimitDb) || double.IsInfinity(request.MinLimitDb))
        {
            throw new SfftAnalysisException(
                SfftAnalysisErrorCode.InvalidMinLimitDb,
                request.MinLimitDb.ToString(CultureInfo.InvariantCulture));
        }

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
            .ConfigureAwait(false);

        SfftWindowAnalyzer analyzer = new(
            streamInfo.SampleRate,
            streamInfo.Channels,
            request.Name,
            request.WindowSizeMs,
            request.HopMs,
            request.BinCount,
            request.MinLimitDb,
            pointWriter);

        await pcmFrameReader
            .ReadFramesAsync(
                toolPaths,
                request.InputFilePath,
                streamInfo.Channels,
                analyzer,
                cancellationToken)
            .ConfigureAwait(false);

        return analyzer.BuildSummary();
    }
}
