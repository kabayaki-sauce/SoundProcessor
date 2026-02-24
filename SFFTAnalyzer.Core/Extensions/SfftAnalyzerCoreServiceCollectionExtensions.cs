using AudioProcessor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SFFTAnalyzer.Core.Application.UseCases;

namespace SFFTAnalyzer.Core.Extensions;

public static class SfftAnalyzerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddSfftAnalyzerCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAudioProcessor();
        services.AddSingleton<SfftAnalysisUseCase>();

        return services;
    }
}
