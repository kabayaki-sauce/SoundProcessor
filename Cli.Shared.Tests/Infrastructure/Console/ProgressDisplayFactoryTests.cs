using CliShared.Application.Ports;
using CliShared.Infrastructure.Console;

namespace CliShared.Tests.Infrastructure.Console;

public sealed class ProgressDisplayFactoryTests
{
    [Fact]
    public void Create_ShouldReturnNoOp_WhenDisabled()
    {
        ProgressDisplayFactory factory = new(new FakeConsoleEnvironment(isErrorRedirected: false, isUserInteractive: true));

        IProgressDisplay display = factory.Create(enabled: false);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenErrorIsRedirected()
    {
        ProgressDisplayFactory factory = new(new FakeConsoleEnvironment(isErrorRedirected: true, isUserInteractive: true));

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenEnvironmentIsNonInteractive()
    {
        ProgressDisplayFactory factory = new(new FakeConsoleEnvironment(isErrorRedirected: false, isUserInteractive: false));

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnDualLineDisplay_WhenEnabledAndInteractive()
    {
        ProgressDisplayFactory factory = new(new FakeConsoleEnvironment(isErrorRedirected: false, isUserInteractive: true));

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.IsType<DualLineProgressDisplay>(display);
    }

    private sealed class FakeConsoleEnvironment : IConsoleEnvironment
    {
        public FakeConsoleEnvironment(bool isErrorRedirected, bool isUserInteractive)
        {
            IsErrorRedirected = isErrorRedirected;
            IsUserInteractive = isUserInteractive;
        }

        public bool IsErrorRedirected { get; }

        public bool IsUserInteractive { get; }

        public TextWriter ErrorWriter { get; } = new StringWriter();
    }
}
