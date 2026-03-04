using STFTAnalyzer.Core.Domain.Models;

namespace STFTAnalyzer.Core.Application.Ports;

public interface IStftInferenceNowTensorPointWriter
{
    public void Write(StftInferenceNowTensorPoint point);
}
