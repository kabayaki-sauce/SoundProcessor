using AudioProcessor.Application.Models;

namespace AudioProcessor.Application.Ports;

public interface IAudioSegmentExporter
{
    public Task ExportAsync(
        FfmpegToolPaths toolPaths,
        SegmentExportRequest request,
        CancellationToken cancellationToken);
}
