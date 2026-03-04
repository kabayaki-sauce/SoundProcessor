namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal sealed class MelSpectrogramAnalysisExecutionResult
{
    public MelSpectrogramAnalysisExecutionResult(
        MelSpectrogramAnalysisBatchSummary summary,
        IReadOnlyList<string> warnings)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
    }

    public MelSpectrogramAnalysisBatchSummary Summary { get; }

    public IReadOnlyList<string> Warnings { get; }
}
