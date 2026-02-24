using AudioSplitter.Core.Application.Errors;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Cli.Presentation.Cli.Texts;

namespace AudioSplitter.Cli.Infrastructure.Console;

internal sealed class OverwriteConfirmationService : IOverwriteConfirmationService
{
    public OverwriteDecision Resolve(string outputPath, bool overwriteWithoutPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        bool fileExists = File.Exists(outputPath);
        if (!fileExists)
        {
            return new OverwriteDecision(true, false);
        }

        if (overwriteWithoutPrompt)
        {
            return new OverwriteDecision(true, false);
        }

        if (System.Console.IsInputRedirected || !Environment.UserInteractive)
        {
            throw new SplitAudioException(
                SplitAudioErrorCode.OverwriteConflictInNonInteractive,
                ConsoleTexts.OverwriteConflictInNonInteractive);
        }

        while (true)
        {
            System.Console.Error.Write(ConsoleTexts.PathWithSuffix(outputPath, ConsoleTexts.OverwritePromptSuffix));
            string? answer = System.Console.ReadLine();
            if (string.IsNullOrWhiteSpace(answer))
            {
                return new OverwriteDecision(false, true);
            }

            string normalized = answer.Trim();
            if (string.Equals(normalized, ConsoleTexts.YesAnswer, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, ConsoleTexts.YesLongAnswer, StringComparison.OrdinalIgnoreCase))
            {
                return new OverwriteDecision(true, true);
            }

            if (string.Equals(normalized, ConsoleTexts.NoAnswer, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, ConsoleTexts.NoLongAnswer, StringComparison.OrdinalIgnoreCase))
            {
                return new OverwriteDecision(false, true);
            }

            System.Console.Error.WriteLine(ConsoleTexts.OverwriteAnswerInvalid);
        }
    }
}
