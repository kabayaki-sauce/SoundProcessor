namespace STFTAnalyzer.Core.Application.Models;

public sealed class StftInferenceFileRequest
{
    public StftInferenceFileRequest(
        string inputFilePath,
        string name,
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
        string? ffmpegPath,
        int processingThreads = 1,
        StftInferenceChannelHandlingMode channelHandlingMode = StftInferenceChannelHandlingMode.DuplicateMonoAndTakeFirstTwo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentDurationSeconds);
        ArgumentNullException.ThrowIfNull(nowMsList);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nFft);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(winLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingThreads);

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

        InputFilePath = inputFilePath;
        Name = name;
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
        FfmpegPath = ffmpegPath;
        ProcessingThreads = processingThreads;
        ChannelHandlingMode = channelHandlingMode;
    }

    public string InputFilePath { get; }

    public string Name { get; }

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

    public string? FfmpegPath { get; }

    public int ProcessingThreads { get; }

    public StftInferenceChannelHandlingMode ChannelHandlingMode { get; }
}
