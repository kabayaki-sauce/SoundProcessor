using System.Globalization;
using AudioSplitter.Core.Domain.ValueObjects;
using AudioSplitter.Cli.Presentation.Cli.Texts;

namespace AudioSplitter.Cli.Presentation.Cli.Arguments;

internal static class CommandLineParser
{
    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return CommandLineParseResult.Help();
        }

        string? inputFilePath = null;
        string? outputDirectoryPath = null;
        string? levelText = null;
        string? durationText = null;
        string? afterOffsetText = null;
        string? resumeOffsetText = null;
        string? resolutionTypeText = null;
        string? ffmpegPath = null;
        bool overwriteWithoutPrompt = false;

        List<string> errors = new();

        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (IsHelp(token))
            {
                return CommandLineParseResult.Help();
            }

            if (string.Equals(token, ConsoleTexts.OverwriteOption, StringComparison.OrdinalIgnoreCase))
            {
                overwriteWithoutPrompt = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.InputFileOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                inputFilePath = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.OutputDirOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                outputDirectoryPath = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.LevelOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                levelText = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.DurationOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                durationText = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.AfterOffsetOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                afterOffsetText = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ResumeOffsetOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                resumeOffsetText = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ResolutionTypeOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                resolutionTypeText = value;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.FfmpegPathOption))
            {
                if (!TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    continue;
                }

                ffmpegPath = value;
                continue;
            }

            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.UnknownOptionPrefix, token));
        }

        if (inputFilePath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.InputFileOption));
        }

        if (outputDirectoryPath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.OutputDirOption));
        }

        if (levelText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.LevelOption));
        }

        if (durationText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.DurationOption));
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        string levelValue = levelText!;
        string durationValue = durationText!;

        if (!double.TryParse(levelValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double levelDb))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidNumberPrefix, levelValue));
        }
        else if (levelDb >= 0)
        {
            errors.Add(ConsoleTexts.InvalidLevelText);
        }

        bool parsedDuration = TryParseTime(durationValue, out TimeSpan duration);
        if (!parsedDuration)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, durationValue));
        }
        else if (duration <= TimeSpan.Zero)
        {
            errors.Add(ConsoleTexts.InvalidDurationText);
        }

        TimeSpan afterOffset = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(afterOffsetText))
        {
            string afterOffsetValue = afterOffsetText!;
            bool parsedAfterOffset = TryParseTime(afterOffsetValue, out afterOffset);
            if (!parsedAfterOffset)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, afterOffsetValue));
            }
            else if (afterOffset < TimeSpan.Zero)
            {
                errors.Add(ConsoleTexts.InvalidAfterOffsetText);
            }
        }

        TimeSpan resumeOffset = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(resumeOffsetText))
        {
            string resumeOffsetValue = resumeOffsetText!;
            bool parsedResumeOffset = TryParseTime(resumeOffsetValue, out resumeOffset);
            if (!parsedResumeOffset)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, resumeOffsetValue));
            }
        }

        ResolutionType? resolutionType = null;
        if (!string.IsNullOrWhiteSpace(resolutionTypeText))
        {
            string resolutionTypeValue = resolutionTypeText!;
            bool parsedResolutionType = ResolutionType.TryParse(resolutionTypeValue, out ResolutionType parsedValue);
            if (!parsedResolutionType)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidResolutionPrefix, resolutionTypeValue));
            }
            else
            {
                resolutionType = parsedValue;
            }
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        CommandLineArguments parsedArguments = new(
            inputFilePath!,
            outputDirectoryPath!,
            levelDb,
            duration,
            afterOffset,
            resumeOffset,
            resolutionType,
            ffmpegPath,
            overwriteWithoutPrompt);
        return CommandLineParseResult.Success(parsedArguments);
    }

    private static bool MatchesOption(string token, string optionName)
    {
        return string.Equals(token, optionName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelp(string token)
    {
        return string.Equals(token, ConsoleTexts.HelpOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, ConsoleTexts.ShortHelpOption, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        List<string> errors,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(optionName);
        ArgumentNullException.ThrowIfNull(errors);

        value = string.Empty;

        int valueIndex = index + 1;
        if (valueIndex >= args.Count)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingValuePrefix, optionName));
            return false;
        }

        value = args[valueIndex];
        index = valueIndex;
        return true;
    }

    private static bool TryParseTime(string text, out TimeSpan value)
    {
        if (!TimeArgument.TryParse(text, out TimeArgument argument))
        {
            value = default;
            return false;
        }

        value = argument.Value;
        return true;
    }
}
