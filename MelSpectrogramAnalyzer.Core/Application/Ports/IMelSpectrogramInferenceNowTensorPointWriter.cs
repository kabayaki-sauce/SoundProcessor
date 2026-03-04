using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Application.Ports;

public interface IMelSpectrogramInferenceNowTensorPointWriter
{
    public void Write(MelSpectrogramInferenceNowTensorPoint point);
}
