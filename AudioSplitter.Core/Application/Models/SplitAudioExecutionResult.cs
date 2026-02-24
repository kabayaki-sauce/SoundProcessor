using AudioSplitter.Core.Domain.Models;

namespace AudioSplitter.Core.Application.Models;

public sealed class SplitAudioExecutionResult
{
    public SplitAudioExecutionResult(SplitExecutionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
    }

    public SplitExecutionSummary Summary { get; }
}
