using AudioProcessor.Domain.Models;

namespace AudioProcessor.Tests.Domain;

public sealed class OutputAudioFormatTests
{
    [Fact]
    public void FromInputStream_ShouldKeepBitDepthAndSampleRate()
    {
        AudioStreamInfo streamInfo = new(44100, 2, AudioPcmBitDepth.Pcm24, 1000);

        OutputAudioFormat format = OutputAudioFormat.FromInputStream(streamInfo);

        Assert.Equal(AudioPcmBitDepth.Pcm24, format.BitDepth);
        Assert.Equal(44100, format.SampleRate);
        Assert.Equal("pcm_s24le", format.CodecName);
    }
}
