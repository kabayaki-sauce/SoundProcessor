namespace MelSpectrogramAnalyzer.Core.Application.Models;

public sealed class MelSpectrogramInferenceFileRequest
{
    public MelSpectrogramInferenceFileRequest(
        string inputFilePath,
        string name,
        int sampleRate,
        double segmentDurationSeconds,
        IReadOnlyList<int> nowMsList,
        int nFft,
        int winLength,
        int hopLength,
        int nMels,
        double fMinHz,
        double fMaxHz,
        double melPower,
        MelSpectrogramScaleKind melScale,
        MelSpectrogramInferenceNormKind melNorm,
        bool center,
        MelSpectrogramInferencePadMode padMode,
        bool leftPadNoiseEnabled,
        double leftPadNoiseDb,
        bool emitLinear,
        bool emitDb,
        double? sanitizeMinDbfs,
        string? ffmpegPath,
        int processingThreads = 1,
        MelSpectrogramInferenceChannelHandlingMode channelHandlingMode = MelSpectrogramInferenceChannelHandlingMode.DuplicateMonoAndTakeFirstTwo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentDurationSeconds);
        ArgumentNullException.ThrowIfNull(nowMsList);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nFft);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(winLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hopLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nMels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingThreads);

        if (nowMsList.Count == 0)
        {
            throw new ArgumentException("nowMsList must contain at least one point.", nameof(nowMsList));
        }

        if (winLength > nFft)
        {
            throw new ArgumentOutOfRangeException(nameof(winLength), "winLength must not exceed nFft.");
        }

        if (double.IsNaN(melPower) || double.IsInfinity(melPower) || melPower <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(melPower));
        }

        if (double.IsNaN(fMinHz) || double.IsInfinity(fMinHz) || fMinHz < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fMinHz));
        }

        if (double.IsNaN(fMaxHz) || double.IsInfinity(fMaxHz) || fMaxHz <= fMinHz)
        {
            throw new ArgumentOutOfRangeException(nameof(fMaxHz));
        }

        double nyquist = sampleRate / 2.0;
        if (fMaxHz > nyquist)
        {
            throw new ArgumentOutOfRangeException(nameof(fMaxHz), "fMaxHz must not exceed Nyquist.");
        }

        if (!Enum.IsDefined(melScale))
        {
            throw new ArgumentOutOfRangeException(nameof(melScale));
        }

        if (!Enum.IsDefined(melNorm))
        {
            throw new ArgumentOutOfRangeException(nameof(melNorm));
        }

        if (!Enum.IsDefined(padMode))
        {
            throw new ArgumentOutOfRangeException(nameof(padMode));
        }

        if (!Enum.IsDefined(channelHandlingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(channelHandlingMode));
        }

        if (double.IsNaN(leftPadNoiseDb) || double.IsInfinity(leftPadNoiseDb))
        {
            throw new ArgumentOutOfRangeException(nameof(leftPadNoiseDb));
        }

        if (!emitLinear && !emitDb)
        {
            throw new ArgumentException("At least one output kind must be enabled.");
        }

        if (sanitizeMinDbfs.HasValue && (double.IsNaN(sanitizeMinDbfs.Value) || double.IsInfinity(sanitizeMinDbfs.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(sanitizeMinDbfs));
        }

        InputFilePath = inputFilePath;
        Name = name;
        SampleRate = sampleRate;
        SegmentDurationSeconds = segmentDurationSeconds;
        NowMsList = nowMsList;
        NFft = nFft;
        WinLength = winLength;
        HopLength = hopLength;
        NMels = nMels;
        FMinHz = fMinHz;
        FMaxHz = fMaxHz;
        MelPower = melPower;
        MelScale = melScale;
        MelNorm = melNorm;
        Center = center;
        PadMode = padMode;
        LeftPadNoiseEnabled = leftPadNoiseEnabled;
        LeftPadNoiseDb = leftPadNoiseDb;
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

    public int NMels { get; }

    public double FMinHz { get; }

    public double FMaxHz { get; }

    public double MelPower { get; }

    public MelSpectrogramScaleKind MelScale { get; }

    public MelSpectrogramInferenceNormKind MelNorm { get; }

    public bool Center { get; }

    public MelSpectrogramInferencePadMode PadMode { get; }

    public bool LeftPadNoiseEnabled { get; }

    public double LeftPadNoiseDb { get; }

    public bool EmitLinear { get; }

    public bool EmitDb { get; }

    public double? SanitizeMinDbfs { get; }

    public string? FfmpegPath { get; }

    public int ProcessingThreads { get; }

    public MelSpectrogramInferenceChannelHandlingMode ChannelHandlingMode { get; }
}
