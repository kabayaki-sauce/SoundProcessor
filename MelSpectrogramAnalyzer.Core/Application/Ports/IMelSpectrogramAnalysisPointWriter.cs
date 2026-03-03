using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Application.Ports;

public interface IMelSpectrogramAnalysisPointWriter
{
    public void Write(MelSpectrogramAnalysisPoint point);
}

