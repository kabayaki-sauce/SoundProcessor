using AudioProcessor.Domain.Models;

namespace AudioProcessor.Application.Models;

public sealed class SegmentExportRequest
{
    public SegmentExportRequest(
        string inputFilePath,
        string outputFilePath,
        AudioSegment segment,
        OutputAudioFormat outputFormat,
        int inputSampleRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(outputFormat);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputSampleRate);

        InputFilePath = inputFilePath;
        OutputFilePath = outputFilePath;
        Segment = segment;
        OutputFormat = outputFormat;
        InputSampleRate = inputSampleRate;
    }

    public string InputFilePath { get; }

    public string OutputFilePath { get; }

    public AudioSegment Segment { get; }

    public OutputAudioFormat OutputFormat { get; }

    public int InputSampleRate { get; }
}
