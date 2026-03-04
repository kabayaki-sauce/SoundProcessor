using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using STFTAnalyzer.Core.Application.Errors;
using STFTAnalyzer.Core.Application.Models;
using STFTAnalyzer.Core.Application.Ports;
using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Application.UseCases;

public sealed class StftInferenceFileFeatureUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;
    private readonly StftInferenceWaveformFeatureUseCase waveformFeatureUseCase;

    public StftInferenceFileFeatureUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader,
        StftInferenceWaveformFeatureUseCase waveformFeatureUseCase)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
        this.waveformFeatureUseCase = waveformFeatureUseCase ?? throw new ArgumentNullException(nameof(waveformFeatureUseCase));
    }

    public async Task<StftInferenceFeatureSummary> ExecuteAsync(
        StftInferenceFileRequest request,
        IStftInferenceNowTensorPointWriter? nowTensorPointWriter,
        IStftInferenceFramePointWriter? framePointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.InputFilePath))
        {
            throw new StftInferenceException(StftInferenceErrorCode.InputFileNotFound, request.InputFilePath);
        }

        if (nowTensorPointWriter is null && framePointWriter is null)
        {
            throw new ArgumentException("At least one writer must be provided.");
        }

        FfmpegToolPaths toolPaths;
        try
        {
            toolPaths = ffmpegLocator.Resolve(request.FfmpegPath);
        }
        catch (Exception ex)
        {
            throw new StftInferenceException(StftInferenceErrorCode.InvalidConfiguration, "Failed to resolve ffmpeg tools.", ex);
        }

        AudioStreamInfo streamInfo;
        try
        {
            streamInfo = await audioProbeService
                .ProbeAsync(toolPaths, request.InputFilePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new StftInferenceException(StftInferenceErrorCode.ProbeFailed, request.InputFilePath, ex);
        }

        CapturingPcmFrameSink sink = new(streamInfo.Channels);
        try
        {
            int? targetSampleRateHz = request.SampleRate == streamInfo.SampleRate
                ? null
                : request.SampleRate;

            await pcmFrameReader
                .ReadFramesAsync(
                    toolPaths,
                    request.InputFilePath,
                    streamInfo.Channels,
                    sink,
                    cancellationToken,
                    targetSampleRateHz)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new StftInferenceException(StftInferenceErrorCode.FrameReadFailed, request.InputFilePath, ex);
        }

        StftInferenceWaveformRequest waveformRequest = new(
            request.Name,
            sink.BuildWaveform(),
            request.SampleRate,
            request.SegmentDurationSeconds,
            request.NowMsList,
            request.NFft,
            request.WinLength,
            request.HopLength,
            request.Power,
            request.Center,
            request.PadMode,
            request.EmitLinear,
            request.EmitDb,
            request.SanitizeMinDbfs,
            request.ProcessingThreads,
            request.ChannelHandlingMode);

        return await waveformFeatureUseCase
            .ExecuteAsync(waveformRequest, nowTensorPointWriter, framePointWriter, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class CapturingPcmFrameSink : IAudioPcmFrameSink
    {
        private readonly List<float>[] channels;

        public CapturingPcmFrameSink(int channelCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channelCount);

            channels = new List<float>[channelCount];
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i] = [];
            }
        }

        public void OnFrame(ReadOnlySpan<float> frameSamples)
        {
            if (frameSamples.Length != channels.Length)
            {
                throw new ArgumentException("Unexpected channel count.", nameof(frameSamples));
            }

            for (int channel = 0; channel < channels.Length; channel++)
            {
                channels[channel].Add(frameSamples[channel]);
            }
        }

        public float[][] BuildWaveform()
        {
            float[][] waveform = new float[channels.Length][];
            for (int channel = 0; channel < channels.Length; channel++)
            {
                waveform[channel] = channels[channel].ToArray();
            }

            return waveform;
        }
    }
}
