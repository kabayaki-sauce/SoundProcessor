using CliShared.Application.Ports;
using CliShared.Infrastructure.Console;
using Microsoft.Extensions.DependencyInjection;

namespace CliShared.Extensions;

public static class CliSharedServiceCollectionExtensions
{
    public static IServiceCollection AddCliShared(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProgressDisplayFactory, ProgressDisplayFactory>();
        return services;
    }
}
