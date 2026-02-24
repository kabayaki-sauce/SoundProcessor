using AudioProcessor.Extensions;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Core.Application.UseCases;
using AudioSplitter.Core.Infrastructure.Analysis;
using Microsoft.Extensions.DependencyInjection;

namespace AudioSplitter.Core.Extensions;

public static class AudioSplitterCoreServiceCollectionExtensions
{
    public static IServiceCollection AddAudioSplitterCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAudioProcessor();
        services.AddSingleton<SplitAudioUseCase>();
        services.AddSingleton<ISilenceAnalyzer, SilenceAnalyzer>();

        return services;
    }
}
