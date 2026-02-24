using AudioProcessor.Application.Ports;
using AudioProcessor.Infrastructure.Ffmpeg;
using Microsoft.Extensions.DependencyInjection;

namespace AudioProcessor.Extensions;

public static class AudioProcessorServiceCollectionExtensions
{
    public static IServiceCollection AddAudioProcessor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IFfmpegLocator, FfmpegLocator>();
        services.AddSingleton<IAudioProbeService, FfmpegProbeService>();
        services.AddSingleton<IAudioPcmFrameReader, FfmpegPcmFrameReader>();
        services.AddSingleton<IAudioSegmentExporter, FfmpegSegmentExporter>();

        return services;
    }
}
