namespace AudioProcessor.Domain.Models;

public sealed class OutputAudioFormat
{
    public OutputAudioFormat(AudioPcmBitDepth bitDepth, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        BitDepth = bitDepth;
        SampleRate = sampleRate;
    }

    public AudioPcmBitDepth BitDepth { get; }

    public int SampleRate { get; }

    public string CodecName
    {
        get
        {
            return BitDepth switch
            {
                AudioPcmBitDepth.Pcm16 => "pcm_s16le",
                AudioPcmBitDepth.Pcm24 => "pcm_s24le",
                AudioPcmBitDepth.F32 => "pcm_f32le",
                _ => throw new InvalidOperationException(),
            };
        }
    }

    public static OutputAudioFormat FromInputStream(AudioStreamInfo streamInfo)
    {
        ArgumentNullException.ThrowIfNull(streamInfo);
        return new OutputAudioFormat(streamInfo.PcmBitDepth, streamInfo.SampleRate);
    }
}


