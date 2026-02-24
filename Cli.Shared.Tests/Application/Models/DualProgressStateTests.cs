using Cli.Shared.Application.Models;

namespace Cli.Shared.Tests.Application.Models;

public sealed class DualProgressStateTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultLabels_WhenLabelsAreNullOrWhitespace()
    {
        DualProgressState state = new(null!, 0.25, "   ", 0.75);

        Assert.Equal("Progress", state.TopLabel, StringComparer.Ordinal);
        Assert.Equal("Current", state.BottomLabel, StringComparer.Ordinal);
        Assert.Equal(0.25, state.TopRatio, precision: 6);
        Assert.Equal(0.75, state.BottomRatio, precision: 6);
    }

    [Fact]
    public void Constructor_ShouldKeepProvidedLabels_WhenNotBlank()
    {
        DualProgressState state = new("Songs", 0.5, "Current song", 0.1);

        Assert.Equal("Songs", state.TopLabel, StringComparer.Ordinal);
        Assert.Equal("Current song", state.BottomLabel, StringComparer.Ordinal);
        Assert.Equal(0.5, state.TopRatio, precision: 6);
        Assert.Equal(0.1, state.BottomRatio, precision: 6);
    }
}
