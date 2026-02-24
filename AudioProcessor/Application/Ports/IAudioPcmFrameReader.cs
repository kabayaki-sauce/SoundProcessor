using AudioProcessor.Application.Models;

namespace AudioProcessor.Application.Ports;

public interface IAudioPcmFrameReader
{
    public Task ReadFramesAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        int channels,
        IAudioPcmFrameSink frameSink,
        CancellationToken cancellationToken);
}
