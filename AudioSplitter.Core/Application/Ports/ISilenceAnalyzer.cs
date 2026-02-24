using AudioProcessor.Application.Models;
using AudioProcessor.Domain.Models;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Domain.Models;

namespace AudioSplitter.Core.Application.Ports;

public interface ISilenceAnalyzer
{
    public Task<SilenceAnalysisResult> AnalyzeAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        AudioStreamInfo streamInfo,
        double levelDb,
        TimeSpan duration,
        CancellationToken cancellationToken,
        Action<SilenceAnalysisProgress>? progressCallback = null);
}
