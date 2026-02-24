namespace AudioSplitter.Core.Application.Ports;

public interface IOverwriteConfirmationService
{
    public OverwriteDecision Resolve(string outputPath, bool overwriteWithoutPrompt);
}
