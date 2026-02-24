using System.Globalization;
using System.Text.RegularExpressions;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Presentation.Cli.Arguments;

internal static partial class CommandLineParser
{
    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return CommandLineParseResult.Help();
        }

        string? windowSizeText = null;
        string? hopText = null;
        string? inputDirPath = null;
        string? dbFilePath = null;
        string? stemsText = null;
        string? modeText = null;
        string? tableNameOverride = null;
        string? minLimitDbText = null;
        string? binCountText = null;
        string? ffmpegPath = null;
        bool upsert = false;
        bool skipDuplicate = false;
        bool deleteCurrent = false;
        bool recursive = false;
        bool progress = false;

        List<string> errors = new();

        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (IsHelp(token))
            {
                return CommandLineParseResult.Help();
            }

            if (MatchesOption(token, ConsoleTexts.UpsertOption))
            {
                upsert = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.SkipDuplicateOption))
            {
                skipDuplicate = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.DeleteCurrentOption))
            {
                deleteCurrent = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.RecursiveOption))
            {
                recursive = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ProgressOption))
            {
                progress = true;
                continue;
            }

            if (MatchesOption(token, ConsoleTexts.WindowSizeOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    windowSizeText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.HopOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    hopText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.InputDirOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    inputDirPath = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.DbFileOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    dbFilePath = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.StemsOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    stemsText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.ModeOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    modeText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.TableNameOverrideOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    tableNameOverride = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.MinLimitDbOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    minLimitDbText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.BinCountOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    binCountText = value;
                }

                continue;
            }

            if (MatchesOption(token, ConsoleTexts.FfmpegPathOption))
            {
                if (TryReadOptionValue(args, ref i, token, errors, out string value))
                {
                    ffmpegPath = value;
                }

                continue;
            }

            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.UnknownOptionPrefix, token));
        }

        if (windowSizeText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.WindowSizeOption));
        }

        if (hopText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.HopOption));
        }

        if (inputDirPath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.InputDirOption));
        }

        if (dbFilePath is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.DbFileOption));
        }

        if (modeText is null)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.ModeOption));
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        string windowValue = windowSizeText!;
        string hopValue = hopText!;
        string modeValue = modeText!;

        if (!TryParseIntegralMilliseconds(windowValue, out long windowMs))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, windowValue));
        }

        if (!TryParseIntegralMilliseconds(hopValue, out long hopMs))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTimePrefix, hopValue));
        }

        bool isPeakMode = string.Equals(modeValue, ConsoleTexts.PeakAnalysisMode, StringComparison.OrdinalIgnoreCase);
        bool isSfftMode = string.Equals(modeValue, ConsoleTexts.SfftAnalysisMode, StringComparison.OrdinalIgnoreCase);
        if (!isPeakMode && !isSfftMode)
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidModePrefix, modeValue));
        }

        IReadOnlyList<string>? stems = null;
        if (!string.IsNullOrWhiteSpace(stemsText))
        {
            if (!TryParseStems(stemsText!, out List<string> parsedStems))
            {
                errors.Add(ConsoleTexts.InvalidStemsText);
            }
            else
            {
                stems = parsedStems;
            }
        }

        int? binCount = null;
        if (!string.IsNullOrWhiteSpace(binCountText))
        {
            if (!TryParsePositiveInt(binCountText!, out int parsedBinCount))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidIntegerPrefix, binCountText!));
            }
            else
            {
                binCount = parsedBinCount;
            }
        }

        if (isSfftMode)
        {
            if (binCount is null)
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.MissingOptionPrefix, ConsoleTexts.BinCountOption));
            }

            if (stemsText is not null)
            {
                errors.Add(ConsoleTexts.StemsNotSupportedForSfftText);
            }
        }

        if (isPeakMode)
        {
            if (binCount is not null)
            {
                errors.Add(ConsoleTexts.BinCountOnlyForSfftText);
            }

            if (deleteCurrent)
            {
                errors.Add(ConsoleTexts.DeleteCurrentOnlyForSfftText);
            }

            if (recursive)
            {
                errors.Add(ConsoleTexts.RecursiveOnlyForSfftText);
            }
        }

        string defaultTableName = isSfftMode ? ConsoleTexts.DefaultSfftTableName : ConsoleTexts.DefaultPeakTableName;
        string tableName = string.IsNullOrWhiteSpace(tableNameOverride)
            ? defaultTableName
            : tableNameOverride!.Trim();

        if (!IsValidTableName(tableName))
        {
            errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidTableNamePrefix, tableName));
        }

        double minLimitDb = -120.0;
        if (!string.IsNullOrWhiteSpace(minLimitDbText))
        {
            if (!double.TryParse(minLimitDbText, NumberStyles.Float, CultureInfo.InvariantCulture, out minLimitDb)
                || double.IsNaN(minLimitDb)
                || double.IsInfinity(minLimitDb))
            {
                errors.Add(ConsoleTexts.WithValue(ConsoleTexts.InvalidNumberPrefix, minLimitDbText!));
            }
        }

        if (upsert && skipDuplicate)
        {
            errors.Add(ConsoleTexts.UpsertSkipConflictText);
        }

        if (errors.Count > 0)
        {
            return CommandLineParseResult.Failure(errors);
        }

        string mode = isSfftMode ? ConsoleTexts.SfftAnalysisMode : ConsoleTexts.PeakAnalysisMode;
        CommandLineArguments arguments = new(
            windowMs,
            hopMs,
            inputDirPath!,
            dbFilePath!,
            stems,
            mode,
            tableName,
            upsert,
            skipDuplicate,
            minLimitDb,
            binCount,
            deleteCurrent,
            recursive,
            ffmpegPath,
            progress);
        return CommandLineParseResult.Success(arguments);
    }

    internal static bool IsValidTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        return TableNamePattern().IsMatch(tableName.Trim());
    }

    internal static bool TryParseIntegralMilliseconds(string text, out long milliseconds)
    {
        milliseconds = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = TimePattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double scalar)
            || double.IsNaN(scalar)
            || double.IsInfinity(scalar))
        {
            return false;
        }

        string unit = match.Groups["unit"].Value;
        double totalMilliseconds = unit.ToLowerInvariant() switch
        {
            "ms" => scalar,
            "s" => scalar * 1000.0,
            "m" => scalar * 60_000.0,
            _ => double.NaN,
        };

        if (double.IsNaN(totalMilliseconds) || double.IsInfinity(totalMilliseconds) || totalMilliseconds <= 0)
        {
            return false;
        }

        double rounded = Math.Round(totalMilliseconds, MidpointRounding.AwayFromZero);
        if (Math.Abs(totalMilliseconds - rounded) > 1e-9)
        {
            return false;
        }

        if (rounded > long.MaxValue)
        {
            return false;
        }

        milliseconds = checked((long)rounded);
        return true;
    }

    internal static bool TryParsePositiveInt(string text, out int value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return false;
        }

        if (parsed <= 0 || parsed > int.MaxValue)
        {
            return false;
        }

        value = checked((int)parsed);
        return true;
    }

    internal static bool TryParseStems(string text, out List<string> stems)
    {
        ArgumentNullException.ThrowIfNull(text);

        stems = text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return stems.Count > 0;
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

    [GeneratedRegex(
        @"^(?<value>[+-]?\d+(?:\.\d+)?)(?<unit>ms|s|m)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    [GeneratedRegex(
        @"^[A-Za-z_][A-Za-z0-9_-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex TableNamePattern();
}
