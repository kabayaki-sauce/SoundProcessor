using AudioProcessor.Application.Models;
using AudioProcessor.Domain.Models;

namespace AudioProcessor.Application.Ports;

public interface IAudioProbeService
{
    public Task<AudioStreamInfo> ProbeAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        CancellationToken cancellationToken);
}
