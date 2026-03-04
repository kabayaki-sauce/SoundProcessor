using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using MelSpectrogramAnalyzer.Core.Application.Errors;
using MelSpectrogramAnalyzer.Core.Application.Models;
using MelSpectrogramAnalyzer.Core.Application.Ports;
using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Application.UseCases;

public sealed class MelSpectrogramInferenceFileFeatureUseCase
{
    private readonly IFfmpegLocator ffmpegLocator;
    private readonly IAudioProbeService audioProbeService;
    private readonly IAudioPcmFrameReader pcmFrameReader;
    private readonly MelSpectrogramInferenceWaveformFeatureUseCase waveformFeatureUseCase;

    public MelSpectrogramInferenceFileFeatureUseCase(
        IFfmpegLocator ffmpegLocator,
        IAudioProbeService audioProbeService,
        IAudioPcmFrameReader pcmFrameReader,
        MelSpectrogramInferenceWaveformFeatureUseCase waveformFeatureUseCase)
    {
        this.ffmpegLocator = ffmpegLocator ?? throw new ArgumentNullException(nameof(ffmpegLocator));
        this.audioProbeService = audioProbeService ?? throw new ArgumentNullException(nameof(audioProbeService));
        this.pcmFrameReader = pcmFrameReader ?? throw new ArgumentNullException(nameof(pcmFrameReader));
        this.waveformFeatureUseCase = waveformFeatureUseCase ?? throw new ArgumentNullException(nameof(waveformFeatureUseCase));
    }

    public async Task<MelSpectrogramInferenceFeatureSummary> ExecuteAsync(
        MelSpectrogramInferenceFileRequest request,
        IMelSpectrogramInferenceNowTensorPointWriter? nowTensorPointWriter,
        IMelSpectrogramInferenceFramePointWriter? framePointWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.InputFilePath))
        {
            throw new MelSpectrogramInferenceException(MelSpectrogramInferenceErrorCode.InputFileNotFound, request.InputFilePath);
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
            throw new MelSpectrogramInferenceException(
                MelSpectrogramInferenceErrorCode.InvalidConfiguration,
                "Failed to resolve ffmpeg tools.",
                ex);
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
            throw new MelSpectrogramInferenceException(MelSpectrogramInferenceErrorCode.ProbeFailed, request.InputFilePath, ex);
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
            throw new MelSpectrogramInferenceException(MelSpectrogramInferenceErrorCode.FrameReadFailed, request.InputFilePath, ex);
        }

        MelSpectrogramInferenceWaveformRequest waveformRequest = new(
            request.Name,
            sink.BuildWaveform(),
            request.SampleRate,
            request.SegmentDurationSeconds,
            request.NowMsList,
            request.NFft,
            request.WinLength,
            request.HopLength,
            request.NMels,
            request.FMinHz,
            request.FMaxHz,
            request.MelPower,
            request.MelScale,
            request.MelNorm,
            request.Center,
            request.PadMode,
            request.LeftPadNoiseEnabled,
            request.LeftPadNoiseDb,
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
