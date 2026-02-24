using PeakAnalyzer.Core.Application.Ports;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal interface IPeakAnalysisStore : IPeakAnalysisPointWriter, IDisposable
{
    public void Initialize();

    public void Complete();
}
