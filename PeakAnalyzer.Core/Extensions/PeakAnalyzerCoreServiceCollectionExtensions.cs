using AudioProcessor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using PeakAnalyzer.Core.Application.UseCases;

namespace PeakAnalyzer.Core.Extensions;

public static class PeakAnalyzerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddPeakAnalyzerCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAudioProcessor();
        services.AddSingleton<PeakAnalysisUseCase>();

        return services;
    }
}
