using MelSpectrogramAnalyzer.Core.Application.Ports;

namespace SoundAnalyzer.Cli.Infrastructure.Execution;

internal interface IMelSpectrogramAnalysisStore : IMelSpectrogramAnalysisPointWriter, IDisposable
{
    public void Initialize();

    public void Complete();
}
