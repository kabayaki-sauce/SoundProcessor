using Cli.Shared.Infrastructure.Console;

namespace Cli.Shared.Tests.Infrastructure.Console;

public sealed class ProgressBlockRendererWidthResolutionTests
{
    public static TheoryData<int?, string?, int> ResolveWidthCases => new()
    {
        { 0, null, 80 },
        { 0, "101", 100 },
        { 120, null, 119 },
        { 2, null, 60 },
        { 1, null, 80 },
        { null, null, 80 },
        { null, " 88 ", 87 },
        { null, "abc", 80 },
    };

    [Theory]
    [MemberData(nameof(ResolveWidthCases))]
    public void ResolveWidthFromSnapshot_ShouldResolveExpectedWidth(
        int? bufferWidth,
        string? columnsText,
        int expected)
    {
        int actual = ProgressBlockRenderer.ResolveWidthFromSnapshot(bufferWidth, columnsText);
        Assert.Equal(expected, actual);
    }
}
