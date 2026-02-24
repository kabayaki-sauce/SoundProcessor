using AudioProcessor.Application.Models;

namespace AudioProcessor.Application.Ports;

public interface IFfmpegLocator
{
    public FfmpegToolPaths Resolve(string? ffmpegPath);
}
