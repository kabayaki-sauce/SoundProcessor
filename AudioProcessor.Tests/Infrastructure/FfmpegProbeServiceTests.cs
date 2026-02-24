using AudioProcessor.Application.Errors;
using AudioProcessor.Domain.Models;
using AudioProcessor.Infrastructure.Ffmpeg;

namespace AudioProcessor.Tests.Infrastructure;

public sealed class FfmpegProbeServiceTests
{
    [Theory]
    [InlineData("s24", "24", null, AudioPcmBitDepth.Pcm24)]
    [InlineData("s16", "16", null, AudioPcmBitDepth.Pcm16)]
    [InlineData("flt", null, "32", AudioPcmBitDepth.F32)]
    [InlineData("s32p", "24", "32", AudioPcmBitDepth.Pcm24)]
    public void ParseProbeResult_ShouldResolveBitDepth(
        string sampleFormat,
        string? bitsPerRawSample,
        string? bitsPerSample,
        AudioPcmBitDepth expected)
    {
        string json = BuildProbeJson(44100, 2, sampleFormat, bitsPerRawSample, bitsPerSample, "120.0");

        AudioStreamInfo streamInfo = FfmpegProbeService.ParseProbeResult(json);

        Assert.Equal(expected, streamInfo.PcmBitDepth);
        Assert.Equal(44100, streamInfo.SampleRate);
    }

    [Fact]
    public void ParseProbeResult_ShouldThrowForUnsupportedSampleFormat()
    {
        string json = BuildProbeJson(48000, 2, "dbl", null, null, "10.0");

        AudioProcessorException exception = Assert.Throws<AudioProcessorException>(() => FfmpegProbeService.ParseProbeResult(json));

        Assert.Equal(AudioProcessorErrorCode.UnsupportedSampleFormat, exception.ErrorCode);
    }

    private static string BuildProbeJson(
        int sampleRate,
        int channels,
        string sampleFormat,
        string? bitsPerRawSample,
        string? bitsPerSample,
        string duration)
    {
        string bitsRawPart = bitsPerRawSample is null ? string.Empty : $",\"bits_per_raw_sample\":\"{bitsPerRawSample}\"";
        string bitsPart = bitsPerSample is null ? string.Empty : $",\"bits_per_sample\":\"{bitsPerSample}\"";

        return "{"
            + "\"streams\":[{"
            + $"\"sample_rate\":\"{sampleRate}\","
            + $"\"channels\":\"{channels}\","
            + $"\"sample_fmt\":\"{sampleFormat}\""
            + bitsRawPart
            + bitsPart
            + "}],"
            + "\"format\":{"
            + $"\"duration\":\"{duration}\""
            + "}}";
    }
}


