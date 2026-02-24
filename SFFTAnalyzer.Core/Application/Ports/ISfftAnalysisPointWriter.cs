using SFFTAnalyzer.Core.Domain.Models;

namespace SFFTAnalyzer.Core.Application.Ports;

public interface ISfftAnalysisPointWriter
{
    public void Write(SfftAnalysisPoint point);
}
