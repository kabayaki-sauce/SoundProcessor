using System.Globalization;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using PeakAnalyzer.Core.Application.Errors;
using PeakAnalyzer.Core.Application.Models;
using PeakAnalyzer.Core.Application.Ports;
using PeakAnalyzer.Core.Domain.Models;
using PeakAnalyzer.Core.Infrastructure.Analysis;

namespace PeakAnalyzer.Core.Application.UseCases;

public sealed class PeakAnalysisUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;

    public PeakAnalysisUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
    }

    public async Task<PeakAnalysisSummary> ExecuteAsync(
        PeakAnalysisRequest request,
        IPeakAnalysisPointWriter pointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pointWriter);

        if (!File.Exists(request.InputFilePath))
        {
            throw new PeakAnalysisException(PeakAnalysisErrorCode.InputFileNotFound, request.InputFilePath);
        }

        if (request.WindowSizeMs <= 0)
        {
            throw new PeakAnalysisException(
                PeakAnalysisErrorCode.InvalidWindowSize,
                request.WindowSizeMs.ToString(CultureInfo.InvariantCulture));
        }

        if (request.HopMs <= 0)
        {
            throw new PeakAnalysisException(
                PeakAnalysisErrorCode.InvalidHop,
                request.HopMs.ToString(CultureInfo.InvariantCulture));
        }

        if (double.IsNaN(request.MinLimitDb) || double.IsInfinity(request.MinLimitDb))
        {
            throw new PeakAnalysisException(
                PeakAnalysisErrorCode.InvalidMinLimitDb,
                request.MinLimitDb.ToString(CultureInfo.InvariantCulture));
        }

        FfmpegToolPaths toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        AudioStreamInfo streamInfo = await audioProbeService
            .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
            .ConfigureAwait(false);

        PeakWindowAnalyzer analyzer = new(
            streamInfo.SampleRate,
            request.Name,
            request.Stem,
            request.WindowSizeMs,
            request.HopMs,
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
