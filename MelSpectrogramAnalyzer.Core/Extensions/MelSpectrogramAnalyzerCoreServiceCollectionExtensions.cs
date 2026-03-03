using AudioProcessor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MelSpectrogramAnalyzer.Core.Application.UseCases;

namespace MelSpectrogramAnalyzer.Core.Extensions;

public static class MelSpectrogramAnalyzerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddMelSpectrogramAnalyzerCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAudioProcessor();
        services.AddSingleton<MelSpectrogramAnalysisUseCase>();

        return services;
    }
}

