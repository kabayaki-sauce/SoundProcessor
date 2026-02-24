using System.Diagnostics;
using System.Globalization;
using AudioProcessor.Application.Errors;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;
using AudioProcessor.Domain.Services;

namespace AudioProcessor.Infrastructure.Ffmpeg;

public sealed class FfmpegSegmentExporter : IAudioSegmentExporter
{
    public async Task ExportAsync(
        FfmpegToolPaths toolPaths,
        SegmentExportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentNullException.ThrowIfNull(request);

        ProcessStartInfo startInfo = new()
        {
            FileName = toolPaths.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        string filter = BuildFilter(request);

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(request.InputFilePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("a:0");
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-af");
        startInfo.ArgumentList.Add(filter);
        startInfo.ArgumentList.Add("-acodec");
        startInfo.ArgumentList.Add(request.OutputFormat.CodecName);
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add(request.OutputFormat.SampleRate.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(request.OutputFilePath);

        using Process process = Process.Start(startInfo)
            ?? throw new AudioProcessorException(AudioProcessorErrorCode.ExportFailed, request.OutputFilePath);

        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string errorText = string.IsNullOrWhiteSpace(standardError)
                ? request.OutputFilePath
                : standardError.Trim();
            throw new AudioProcessorException(AudioProcessorErrorCode.ExportFailed, errorText);
        }
    }

    private static string BuildFilter(SegmentExportRequest request)
    {
        string startSeconds = FrameMath.ToInvariantSeconds(request.Segment.StartFrame, request.InputSampleRate);
        string endSeconds = FrameMath.ToInvariantSeconds(request.Segment.EndFrame, request.InputSampleRate);
        return string.Concat("atrim=start=", startSeconds, ":end=", endSeconds, ",asetpts=PTS-STARTPTS");
    }
}
