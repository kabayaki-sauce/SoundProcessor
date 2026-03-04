using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Application.Ports;

public interface IStftInferenceFramePointWriter
{
    public void Write(StftInferenceFramePoint point);
}
