#pragma warning disable CA1819
#pragma warning disable S2368
namespace STFTAnalyzer.Core.Application.Models;

public sealed class StftInferenceWaveformRequest
{
    public StftInferenceWaveformRequest(
        string name,
        float[][] waveform,
        int sampleRate,
        double segmentDurationSeconds,
        IReadOnlyList<int> nowMsList,
        int nFft,
        int winLength,
        int hopLength,
        double power,
        bool center,
        StftInferencePadMode padMode,
        bool emitLinear,
        bool emitDb,
        double? sanitizeMinDbfs,
        int processingThreads = 1,
        StftInferenceChannelHandlingMode channelHandlingMode = StftInferenceChannelHandlingMode.DuplicateMonoAndTakeFirstTwo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(waveform);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentDurationSeconds);
        ArgumentNullException.ThrowIfNull(nowMsList);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nFft);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(winLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingThreads);

        if (waveform.Length == 0)
        {
            throw new ArgumentException("Waveform must contain at least one channel.", nameof(waveform));
        }

        if (nowMsList.Count == 0)
        {
            throw new ArgumentException("nowMsList must contain at least one point.", nameof(nowMsList));
        }

        if (winLength > nFft)
        {
            throw new ArgumentOutOfRangeException(nameof(winLength), "winLength must not exceed nFft.");
        }

        if (double.IsNaN(power) || double.IsInfinity(power) || power <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(power));
        }

        if (!emitLinear && !emitDb)
        {
            throw new ArgumentException("At least one output kind must be enabled.");
        }

        if (sanitizeMinDbfs.HasValue && (double.IsNaN(sanitizeMinDbfs.Value) || double.IsInfinity(sanitizeMinDbfs.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(sanitizeMinDbfs));
        }

        if (!Enum.IsDefined(padMode))
        {
            throw new ArgumentOutOfRangeException(nameof(padMode));
        }

        if (!Enum.IsDefined(channelHandlingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(channelHandlingMode));
        }

        int? sampleCount = null;
        for (int i = 0; i < waveform.Length; i++)
        {
            float[] channel = waveform[i] ?? throw new ArgumentException("Waveform channel must not be null.", nameof(waveform));
            if (sampleCount is null)
            {
                sampleCount = channel.Length;
            }
            else if (sampleCount.Value != channel.Length)
            {
                throw new ArgumentException("All waveform channels must have same sample length.", nameof(waveform));
            }
        }

        Name = name;
        Waveform = waveform;
        SampleRate = sampleRate;
        SegmentDurationSeconds = segmentDurationSeconds;
        NowMsList = nowMsList;
        NFft = nFft;
        WinLength = winLength;
        HopLength = hopLength;
        Power = power;
        Center = center;
        PadMode = padMode;
        EmitLinear = emitLinear;
        EmitDb = emitDb;
        SanitizeMinDbfs = sanitizeMinDbfs;
        ProcessingThreads = processingThreads;
        ChannelHandlingMode = channelHandlingMode;
    }

    public string Name { get; }

    public float[][] Waveform { get; }

    public int SampleRate { get; }

    public double SegmentDurationSeconds { get; }

    public IReadOnlyList<int> NowMsList { get; }

    public int NFft { get; }

    public int WinLength { get; }

    public int HopLength { get; }

    public double Power { get; }

    public bool Center { get; }

    public StftInferencePadMode PadMode { get; }

    public bool EmitLinear { get; }

    public bool EmitDb { get; }

    public double? SanitizeMinDbfs { get; }

    public int ProcessingThreads { get; }

    public StftInferenceChannelHandlingMode ChannelHandlingMode { get; }
}
#pragma warning restore S2368
#pragma warning restore CA1819
