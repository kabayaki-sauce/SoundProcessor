using AudioProcessor.Domain.Models;
using SoundAnalyzer.Cli.Infrastructure.Execution;

namespace SoundAnalyzer.Cli.Tests.Infrastructure.Execution;

public sealed class BatchExecutionSupportTests
{
    [Fact]
    public void TryEstimatePeakPointCountPerTarget_ShouldEstimate_WhenFramesAreAvailable()
    {
        AudioStreamInfo streamInfo = new(
            sampleRate: 48000,
            channels: 2,
            pcmBitDepth: AudioPcmBitDepth.F32,
            estimatedTotalFrames: 480000);

        bool estimated = BatchExecutionSupport.TryEstimatePeakPointCountPerTarget(
            streamInfo,
            hopMs: 10,
            out long pointCount);

        Assert.True(estimated);
        Assert.Equal(1000, pointCount);
    }

    [Fact]
    public void TryEstimatePeakPointCountPerTarget_ShouldReturnFalse_WhenFramesAreMissing()
    {
        AudioStreamInfo streamInfo = new(
            sampleRate: 48000,
            channels: 2,
            pcmBitDepth: AudioPcmBitDepth.F32,
            estimatedTotalFrames: null);

        bool estimated = BatchExecutionSupport.TryEstimatePeakPointCountPerTarget(
            streamInfo,
            hopMs: 10,
            out long pointCount);

        Assert.False(estimated);
        Assert.Equal(0, pointCount);
    }

    [Fact]
    public void TryEstimateStftPointCountPerFile_ShouldEstimate_WhenSampleRateIsUnchanged()
    {
        AudioStreamInfo streamInfo = new(
            sampleRate: 48000,
            channels: 2,
            pcmBitDepth: AudioPcmBitDepth.F32,
            estimatedTotalFrames: 480000);

        bool estimated = BatchExecutionSupport.TryEstimateStftPointCountPerFile(
            streamInfo,
            analysisSampleRate: 48000,
            hopSamples: 480,
            out long pointCount);

        Assert.True(estimated);
        Assert.Equal(2000, pointCount);
    }

    [Fact]
    public void TryEstimateStftPointCountPerFile_ShouldEstimate_WhenSampleRateIsResampled()
    {
        AudioStreamInfo streamInfo = new(
            sampleRate: 48000,
            channels: 2,
            pcmBitDepth: AudioPcmBitDepth.F32,
            estimatedTotalFrames: 480000);

        bool estimated = BatchExecutionSupport.TryEstimateStftPointCountPerFile(
            streamInfo,
            analysisSampleRate: 44100,
            hopSamples: 441,
            out long pointCount);

        Assert.True(estimated);
        Assert.Equal(2000, pointCount);
    }

    [Fact]
    public void TryEstimateStftPointCountPerFile_ShouldReturnFalse_WhenFramesAreMissing()
    {
        AudioStreamInfo streamInfo = new(
            sampleRate: 48000,
            channels: 2,
            pcmBitDepth: AudioPcmBitDepth.F32,
            estimatedTotalFrames: null);

        bool estimated = BatchExecutionSupport.TryEstimateStftPointCountPerFile(
            streamInfo,
            analysisSampleRate: 44100,
            hopSamples: 441,
            out long pointCount);

        Assert.False(estimated);
        Assert.Equal(0, pointCount);
    }
}
