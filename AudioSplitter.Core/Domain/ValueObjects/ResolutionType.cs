using System.Globalization;
using System.Text.RegularExpressions;
using AudioProcessor.Domain.Models;

namespace AudioSplitter.Core.Domain.ValueObjects;

public readonly partial record struct ResolutionType
{
    private ResolutionType(AudioPcmBitDepth bitDepth, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        BitDepth = bitDepth;
        SampleRate = sampleRate;
    }

    public AudioPcmBitDepth BitDepth { get; }

    public int SampleRate { get; }

    public static bool TryParse(string text, out ResolutionType resolutionType)
    {
        resolutionType = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = ResolutionPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        string bitDepthText = match.Groups["bitDepth"].Value.ToLowerInvariant();
        AudioPcmBitDepth bitDepth = bitDepthText switch
        {
            "16bit" => AudioPcmBitDepth.Pcm16,
            "24bit" => AudioPcmBitDepth.Pcm24,
            "32float" => AudioPcmBitDepth.F32,
            _ => default,
        };

        bool sampleRateParsed = int.TryParse(
            match.Groups["sampleRate"].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int sampleRate);
        if (!sampleRateParsed || sampleRate <= 0)
        {
            return false;
        }

        resolutionType = new ResolutionType(bitDepth, sampleRate);
        return true;
    }

    public OutputAudioFormat ToOutputAudioFormat()
    {
        return new OutputAudioFormat(BitDepth, SampleRate);
    }

    [GeneratedRegex(
        @"^(?<bitDepth>16bit|24bit|32float),(?<sampleRate>[1-9]\d*)hz$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionPattern();
}



