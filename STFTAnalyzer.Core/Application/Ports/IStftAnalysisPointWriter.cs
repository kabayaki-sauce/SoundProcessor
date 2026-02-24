using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Application.Ports;

public interface IStftAnalysisPointWriter
{
    public void Write(StftAnalysisPoint point);
}
