using Cli.Shared.Application.Ports;
using Cli.Shared.Extensions;
using Cli.Shared.Infrastructure.Console;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Shared.Tests.Extensions;

public sealed class CliSharedServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCliShared_ShouldRegisterFactoryAsSingleton()
    {
        ServiceCollection services = new();

        _ = services.AddCliShared();
        using ServiceProvider provider = services.BuildServiceProvider();

        IProgressDisplayFactory first = provider.GetRequiredService<IProgressDisplayFactory>();
        IProgressDisplayFactory second = provider.GetRequiredService<IProgressDisplayFactory>();

        Assert.Same(first, second);
        Assert.IsType<ProgressDisplayFactory>(first);
    }
}
