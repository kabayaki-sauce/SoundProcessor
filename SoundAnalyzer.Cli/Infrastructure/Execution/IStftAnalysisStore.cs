using STFTAnalyzer.Core.Application.Ports;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal interface IStftAnalysisStore : IStftAnalysisPointWriter, IDisposable
{
    public void Initialize();

    public void Complete();
}
