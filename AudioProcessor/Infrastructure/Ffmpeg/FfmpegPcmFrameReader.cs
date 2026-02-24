using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using AudioProcessor.Application.Errors;
using AudioProcessor.Application.Models;
using AudioProcessor.Application.Ports;

namespace AudioProcessor.Infrastructure.Ffmpeg;

public sealed class FfmpegPcmFrameReader : IAudioPcmFrameReader
{
    private const int ReadBufferSize = 262_144;

    public async Task ReadFramesAsync(
        FfmpegToolPaths toolPaths,
        string inputFilePath,
        int channels,
        IAudioPcmFrameSink frameSink,
        CancellationToken cancellationToken,
        int? targetSampleRateHz = null)
    {
        ArgumentNullException.ThrowIfNull(toolPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        if (targetSampleRateHz.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRateHz.Value);
        }

        int bytesPerFrame = checked(channels * sizeof(float));

        ProcessStartInfo startInfo = BuildStartInfo(toolPaths.FfmpegPath, inputFilePath, targetSampleRateHz);
        using Process process = Process.Start(startInfo)
            ?? throw new AudioProcessorException(AudioProcessorErrorCode.FrameReadFailed, inputFilePath);

        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        byte[] carryBuffer = new byte[bytesPerFrame];
        int carryLength = 0;
        float[] frameSamples = new float[channels];

        try
        {
            Stream stream = process.StandardOutput.BaseStream;
            while (true)
            {
                int read = await stream
                    .ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                int offset = 0;
                if (carryLength > 0)
                {
                    int consumed = FillCarryAndProcess(
                        readBuffer,
                        read,
                        bytesPerFrame,
                        carryBuffer,
                        ref carryLength,
                        frameSamples,
                        frameSink);
                    offset += consumed;
                }

                int frameByteCount = (read - offset) / bytesPerFrame * bytesPerFrame;
                if (frameByteCount > 0)
                {
                    ProcessFrames(
                        readBuffer.AsSpan(offset, frameByteCount),
                        bytesPerFrame,
                        frameSamples,
                        frameSink);
                    offset += frameByteCount;
                }

                int remainingBytes = read - offset;
                if (remainingBytes > 0)
                {
                    Buffer.BlockCopy(readBuffer, offset, carryBuffer, 0, remainingBytes);
                    carryLength = remainingBytes;
                }
            }

            if (carryLength > 0)
            {
                throw new AudioProcessorException(AudioProcessorErrorCode.IncompleteFrameData, "incomplete");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string errorText = string.IsNullOrWhiteSpace(standardError)
                ? inputFilePath
                : standardError.Trim();
            throw new AudioProcessorException(AudioProcessorErrorCode.FrameReadFailed, errorText);
        }
    }

    private static ProcessStartInfo BuildStartInfo(string ffmpegPath, string inputFilePath, int? targetSampleRateHz)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputFilePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("a:0");
        startInfo.ArgumentList.Add("-vn");

        if (targetSampleRateHz.HasValue)
        {
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(targetSampleRateHz.Value.ToString(CultureInfo.InvariantCulture));
        }

        startInfo.ArgumentList.Add("-acodec");
        startInfo.ArgumentList.Add("pcm_f32le");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("f32le");
        startInfo.ArgumentList.Add("-");
        return startInfo;
    }

    private static int FillCarryAndProcess(
        byte[] buffer,
        int readCount,
        int bytesPerFrame,
        byte[] carryBuffer,
        ref int carryLength,
        float[] frameSamples,
        IAudioPcmFrameSink frameSink)
    {
        int remainingToFill = bytesPerFrame - carryLength;
        int copied = Math.Min(remainingToFill, readCount);

        Buffer.BlockCopy(buffer, 0, carryBuffer, carryLength, copied);
        carryLength += copied;
        if (carryLength == bytesPerFrame)
        {
            ProcessFrame(carryBuffer, frameSamples, frameSink);
            carryLength = 0;
        }

        return copied;
    }

    private static void ProcessFrames(
        ReadOnlySpan<byte> frameBytes,
        int bytesPerFrame,
        float[] frameSamples,
        IAudioPcmFrameSink frameSink)
    {
        for (int offset = 0; offset < frameBytes.Length; offset += bytesPerFrame)
        {
            ReadOnlySpan<byte> frame = frameBytes.Slice(offset, bytesPerFrame);
            ProcessFrame(frame, frameSamples, frameSink);
        }
    }

    private static void ProcessFrame(
        ReadOnlySpan<byte> frame,
        float[] frameSamples,
        IAudioPcmFrameSink frameSink)
    {
        for (int channelIndex = 0; channelIndex < frameSamples.Length; channelIndex++)
        {
            int byteOffset = channelIndex * sizeof(float);
            int bits = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(byteOffset, sizeof(float)));
            frameSamples[channelIndex] = BitConverter.Int32BitsToSingle(bits);
        }

        frameSink.OnFrame(frameSamples);
    }
}
