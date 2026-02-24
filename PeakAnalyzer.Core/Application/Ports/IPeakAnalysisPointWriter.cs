using PeakAnalyzer.Core.Domain.Models;

namespace PeakAnalyzer.Core.Application.Ports;

public interface IPeakAnalysisPointWriter
{
    public void Write(PeakAnalysisPoint point);
}
