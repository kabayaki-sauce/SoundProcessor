using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AudioProcessor.Application.Errors;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Models;

namespace AudioProcessor.Infrastructure.Ffmpeg;

public sealed class FfmpegProbeService : IAudioProbeService
{
    public async Task<AudioStreamInfo> ProbeAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);

        ProcessStartInfo startInfo = new()
        {
            FileName = toolPaths.FfprobePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-select_streams");
        startInfo.ArgumentList.Add("a:0");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("stream=sample_rate,channels,sample_fmt,bits_per_raw_sample,bits_per_sample");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(inputFilePath);

        using Process process = Process.Start(startInfo)
            ?? throw new AudioProcessorException(AudioProcessorErrorCode.ProbeFailed, inputFilePath);

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string standardOutput = await standardOutputTask.ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(standardError)
                ? inputFilePath
                : standardError.Trim();
            throw new AudioProcessorException(AudioProcessorErrorCode.ProbeFailed, message);
        }

        return ParseProbeResult(standardOutput);
    }

    internal static AudioStreamInfo ParseProbeResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new AudioProcessorException(AudioProcessorErrorCode.ProbeFailed, "Empty ffprobe output.");
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("streams", out JsonElement streamArray) || streamArray.GetArrayLength() == 0)
        {
            throw new AudioProcessorException(AudioProcessorErrorCode.ProbeFailed, "Audio stream was not found.");
        }

        JsonElement stream = streamArray[0];
        int sampleRate = ReadRequiredInt(stream, "sample_rate");
        int channels = ReadRequiredInt(stream, "channels");
        string sampleFormat = ReadOptionalString(stream, "sample_fmt");
        int? bitsPerRawSample = ReadOptionalInt(stream, "bits_per_raw_sample");
        int? bitsPerSample = ReadOptionalInt(stream, "bits_per_sample");

        AudioPcmBitDepth bitDepth = ResolvePcmBitDepth(sampleFormat, bitsPerRawSample, bitsPerSample);

        long? estimatedTotalFrames = null;
        if (root.TryGetProperty("format", out JsonElement formatElement))
        {
            string durationText = ReadOptionalString(formatElement, "duration");
            bool parsedDuration = double.TryParse(
                durationText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double durationSeconds);
            if (parsedDuration && durationSeconds > 0)
            {
                long frameCount = checked(
                    (long)Math.Round(durationSeconds * sampleRate, MidpointRounding.AwayFromZero));
                if (frameCount > 0)
                {
                    estimatedTotalFrames = frameCount;
                }
            }
        }

        return new AudioStreamInfo(sampleRate, channels, bitDepth, estimatedTotalFrames);
    }

    private static AudioPcmBitDepth ResolvePcmBitDepth(string sampleFormat, int? bitsPerRawSample, int? bitsPerSample)
    {
        if (ContainsIgnoreCase(sampleFormat, "flt"))
        {
            return AudioPcmBitDepth.F32;
        }

        if (ContainsIgnoreCase(sampleFormat, "s24") || bitsPerRawSample == 24 || bitsPerSample == 24)
        {
            return AudioPcmBitDepth.Pcm24;
        }

        if (ContainsIgnoreCase(sampleFormat, "s16") || bitsPerRawSample == 16 || bitsPerSample == 16)
        {
            return AudioPcmBitDepth.Pcm16;
        }

        if (ContainsIgnoreCase(sampleFormat, "s32"))
        {
            throw new AudioProcessorException(AudioProcessorErrorCode.UnsupportedSampleFormat, sampleFormat);
        }

        if (bitsPerRawSample == 32 || bitsPerSample == 32)
        {
            return AudioPcmBitDepth.F32;
        }

        throw new AudioProcessorException(AudioProcessorErrorCode.UnsupportedSampleFormat, sampleFormat);
    }

    private static bool ContainsIgnoreCase(string value, string keyword)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(keyword);
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadRequiredInt(JsonElement element, string propertyName)
    {
        string value = ReadOptionalString(element, propertyName);
        bool parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result);
        if (!parsed || result <= 0)
        {
            throw new AudioProcessorException(AudioProcessorErrorCode.ProbeFailed, propertyName);
        }

        return result;
    }

    private static int? ReadOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int intValue))
        {
            return intValue;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            bool parsed = int.TryParse(
                property.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedInt);
            if (parsed)
            {
                return parsedInt;
            }
        }

        return null;
    }

    private static string ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.GetRawText();
        }

        return string.Empty;
    }
}


