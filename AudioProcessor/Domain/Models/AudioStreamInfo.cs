using System.Globalization;

namespace AudioProcessor.Domain.Models;

public sealed class AudioStreamInfo
{
    public AudioStreamInfo(
        int sampleRate,
        int channels,
        AudioPcmBitDepth pcmBitDepth,
        long? estimatedTotalFrames)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        if (estimatedTotalFrames.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedTotalFrames.Value);
        }

        SampleRate = sampleRate;
        Channels = channels;
        PcmBitDepth = pcmBitDepth;
        EstimatedTotalFrames = estimatedTotalFrames;
    }

    public int SampleRate { get; }

    public int Channels { get; }

    public AudioPcmBitDepth PcmBitDepth { get; }

    public long? EstimatedTotalFrames { get; }

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{SampleRate}Hz/{Channels}ch/{(int)PcmBitDepth}bit");
    }
}
