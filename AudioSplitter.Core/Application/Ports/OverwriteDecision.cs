namespace AudioSplitter.Core.Application.Ports;

public readonly record struct OverwriteDecision(bool ShouldOverwrite, bool Prompted);
