namespace AudioProcessor.Application.Ports;

public interface IAudioPcmFrameSink
{
    public void OnFrame(ReadOnlySpan<float> frameSamples);
}
