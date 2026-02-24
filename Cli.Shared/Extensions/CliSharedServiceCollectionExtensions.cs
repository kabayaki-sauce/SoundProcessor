using Cli.Shared.Application.Ports;
using Cli.Shared.Infrastructure.Console;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Shared.Extensions;

public static class CliSharedServiceCollectionExtensions
{
    public static IServiceCollection AddCliShared(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProgressDisplayFactory, ProgressDisplayFactory>();
        return services;
    }
}
