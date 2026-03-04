using MelSpectrogramAnalyzer.Core.Domain.Models;

namespace MelSpectrogramAnalyzer.Core.Application.Ports;

public interface IMelSpectrogramInferenceFramePointWriter
{
    public void Write(MelSpectrogramInferenceFramePoint point);
}
