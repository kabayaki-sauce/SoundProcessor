using CliShared.Application.Ports;
using CliShared.Extensions;
using CliShared.Infrastructure.Console;
using Microsoft.Extensions.DependencyInjection;

namespace CliShared.Tests.Extensions;

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
