using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;
using AudioProcessor.Domain.Services;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Core.Domain.Models;

namespace AudioSplitter.Core.Infrastructure.Analysis;

internal sealed class SilenceAnalyzer : ISilenceAnalyzer
{
    private const int ProgressIntervalFrames = 2048;

    private readonly IAudioPcmFrameReader audioPcmFrameReader;

    public SilenceAnalyzer(IAudioPcmFrameReader audioPcmFrameReader)
    {
        this.audioPcmFrameReader = audioPcmFrameReader ?? throw new ArgumentNullException(nameof(audioPcmFrameReader));
    }

    public async Task<SilenceAnalysisResult> AnalyzeAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        AudioStreamInfo streamInfo,
        double levelDb,
        TimeSpan duration,
        CancellationToken cancellationToken,
        Action<SilenceAnalysisProgress>? progressCallback = null)
    {
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentNullException.ThrowIfNull(streamInfo);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        long durationFrameThreshold = FrameMath.DurationToFrameThreshold(duration, streamInfo.SampleRate);
        AnalysisState state = new(durationFrameThreshold, levelDb);

        PeakSink sink = new(state, streamInfo.EstimatedTotalFrames, progressCallback);

        await audioPcmFrameReader
            .ReadFramesAsync(toolPaths, inputFilePath, streamInfo.Channels, sink, cancellationToken)
            .ConfigureAwait(false);

        state.FlushTrailingRun();
        progressCallback?.Invoke(new SilenceAnalysisProgress(state.TotalFrames, streamInfo.EstimatedTotalFrames));
        return new SilenceAnalysisResult(state.TotalFrames, state.FirstSoundFrame, state.SilenceRuns);
    }

    private sealed class PeakSink : IAudioPcmFrameSink
    {
        private readonly AnalysisState state;
        private readonly long? totalFrames;
        private readonly Action<SilenceAnalysisProgress>? progressCallback;
        private long lastReportedFrames;

        public PeakSink(
            AnalysisState state,
            long? totalFrames,
            Action<SilenceAnalysisProgress>? progressCallback)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.totalFrames = totalFrames;
            this.progressCallback = progressCallback;
        }

        public void OnFrame(ReadOnlySpan<float> frameSamples)
        {
            double peak = 0;
            for (int channelIndex = 0; channelIndex < frameSamples.Length; channelIndex++)
            {
                double sample = Math.Abs(frameSamples[channelIndex]);
                if (sample > peak)
                {
                    peak = sample;
                }
            }

            double frameDb = peak <= 0 ? double.NegativeInfinity : 20 * Math.Log10(peak);
            bool isSilent = frameDb < state.LevelDb;
            state.AddFrame(isSilent);
            ReportIfNeeded();
        }

        private void ReportIfNeeded()
        {
            if (progressCallback is null)
            {
                return;
            }

            long processed = state.TotalFrames;
            if (processed - lastReportedFrames < ProgressIntervalFrames)
            {
                return;
            }

            lastReportedFrames = processed;
            progressCallback.Invoke(new SilenceAnalysisProgress(processed, totalFrames));
        }
    }

    private sealed class AnalysisState
    {
        public AnalysisState(long durationFrameThreshold, double levelDb)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationFrameThreshold);

            DurationFrameThreshold = durationFrameThreshold;
            LevelDb = levelDb;
            SilenceRuns = new List<SilenceRun>();
        }

        public long DurationFrameThreshold { get; }

        public double LevelDb { get; }

        public long TotalFrames { get; private set; }

        public long? FirstSoundFrame { get; private set; }

        public List<SilenceRun> SilenceRuns { get; }

        private long? currentRunStartFrame;

        private long currentRunLength;

        public void AddFrame(bool isSilent)
        {
            if (isSilent)
            {
                StartOrExtendSilence();
                TotalFrames++;
                return;
            }

            if (!FirstSoundFrame.HasValue)
            {
                FirstSoundFrame = TotalFrames;
            }

            FlushCurrentRunIfNeeded(TotalFrames);
            TotalFrames++;
        }

        public void FlushTrailingRun()
        {
            FlushCurrentRunIfNeeded(TotalFrames);
        }

        private void StartOrExtendSilence()
        {
            if (!currentRunStartFrame.HasValue)
            {
                currentRunStartFrame = TotalFrames;
                currentRunLength = 1;
                return;
            }

            currentRunLength++;
        }

        private void FlushCurrentRunIfNeeded(long runEndFrameExclusive)
        {
            if (!currentRunStartFrame.HasValue)
            {
                return;
            }

            if (currentRunLength >= DurationFrameThreshold)
            {
                SilenceRuns.Add(new SilenceRun(currentRunStartFrame.Value, runEndFrameExclusive));
            }

            currentRunStartFrame = null;
            currentRunLength = 0;
        }
    }
}
