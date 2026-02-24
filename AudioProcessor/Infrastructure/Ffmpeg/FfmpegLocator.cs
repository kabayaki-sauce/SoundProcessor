using System.ComponentModel;
using System.Diagnostics;
using AudioProcessor.Application.Errors;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;

namespace AudioProcessor.Infrastructure.Ffmpeg;

public sealed class FfmpegLocator : IFfmpegLocator
{
    public FfmpegToolPaths Resolve(string? ffmpegPath)
    {
        (string resolvedFfmpegPath, string resolvedFfprobePath) = ResolvePaths(ffmpegPath);

        EnsureExecutable(resolvedFfmpegPath, AudioProcessorErrorCode.FfmpegNotFound);
        EnsureExecutable(resolvedFfprobePath, AudioProcessorErrorCode.FfprobeNotFound);
        return new FfmpegToolPaths(resolvedFfmpegPath, resolvedFfprobePath);
    }

    private static (string FfmpegPath, string FfprobePath) ResolvePaths(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            return ("ffmpeg", "ffprobe");
        }

        string trimmedPath = ffmpegPath.Trim();
        if (Directory.Exists(trimmedPath))
        {
            string ffmpegBinaryPath = Path.Combine(trimmedPath, ExecutableName("ffmpeg"));
            string ffprobeBinaryPath = Path.Combine(trimmedPath, ExecutableName("ffprobe"));
            return (ffmpegBinaryPath, ffprobeBinaryPath);
        }

        if (File.Exists(trimmedPath))
        {
            string? directoryPath = Path.GetDirectoryName(trimmedPath);
            string ffprobeBinaryPath = directoryPath is null
                ? "ffprobe"
                : Path.Combine(directoryPath, ExecutableName("ffprobe"));
            return (trimmedPath, ffprobeBinaryPath);
        }

        if (IsLikelyPath(trimmedPath))
        {
            string? directoryPath = Path.GetDirectoryName(trimmedPath);
            string ffprobeBinaryPath = directoryPath is null
                ? "ffprobe"
                : Path.Combine(directoryPath, ExecutableName("ffprobe"));
            return (trimmedPath, ffprobeBinaryPath);
        }

        return (trimmedPath, "ffprobe");
    }

    private static bool IsLikelyPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Path.IsPathRooted(path)
            || path.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || path.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string ExecutableName(string baseName)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        return OperatingSystem.IsWindows() ? string.Concat(baseName, ".exe") : baseName;
    }

    private static void EnsureExecutable(string commandPath, AudioProcessorErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(commandPath);

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = commandPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-version");

            using Process process = Process.Start(startInfo)
                ?? throw new AudioProcessorException(errorCode, commandPath);

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new AudioProcessorException(errorCode, commandPath);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new AudioProcessorException(errorCode, commandPath, exception);
        }
    }
}
