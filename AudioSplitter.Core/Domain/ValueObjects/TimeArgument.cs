using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioSplitter.Core.Domain.ValueObjects;

public readonly partial record struct TimeArgument
{
    private TimeArgument(TimeSpan value)
    {
        Value = value;
    }

    public TimeSpan Value { get; }

    public static bool TryParse(string text, out TimeArgument timeArgument)
    {
        timeArgument = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = TimePattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        bool valueParsed = double.TryParse(
            match.Groups["value"].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double scalar);
        if (!valueParsed || double.IsNaN(scalar) || double.IsInfinity(scalar))
        {
            return false;
        }

        string unit = match.Groups["unit"].Value;
        bool converted = TryConvertToTimeSpan(unit, scalar, out TimeSpan result);
        if (!converted)
        {
            return false;
        }

        timeArgument = new TimeArgument(result);
        return true;
    }

    [GeneratedRegex(
        @"^(?<value>[+-]?\d+(?:\.\d+)?)(?<unit>ms|s|m)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    private static bool TryConvertToTimeSpan(string unit, double scalar, out TimeSpan result)
    {
        ArgumentNullException.ThrowIfNull(unit);

        switch (unit.ToLowerInvariant())
        {
            case "ms":
                result = TimeSpan.FromMilliseconds(scalar);
                return true;
            case "s":
                result = TimeSpan.FromSeconds(scalar);
                return true;
            case "m":
                result = TimeSpan.FromMinutes(scalar);
                return true;
            default:
                result = default;
                return false;
        }
    }
}

