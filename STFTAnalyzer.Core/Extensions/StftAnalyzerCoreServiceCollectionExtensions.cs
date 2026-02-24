using AudioProcessor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using STFTAnalyzer.Core.Application.UseCases;

namespace STFTAnalyzer.Core.Extensions;

public static class StftAnalyzerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddStftAnalyzerCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAudioProcessor();
        services.AddSingleton<StftAnalysisUseCase>();

        return services;
    }
}
