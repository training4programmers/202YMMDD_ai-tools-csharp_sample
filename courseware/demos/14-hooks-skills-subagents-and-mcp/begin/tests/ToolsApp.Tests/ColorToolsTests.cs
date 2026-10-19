using ModelContextProtocol;
using ToolsApp.Mcp;

namespace ToolsApp.Tests;

/// <summary>
/// Tests the MCP tool methods directly. No MCP client is involved: a tool is just a method, so the
/// interesting behavior (validation, limits, what gets returned) is plain unit-testable C#.
/// </summary>
public class ColorToolsTests
{
    private readonly FakeColorCatalog _catalog = new()
    {
        Colors =
        [
            new ColorMatch(4, "Deep Sea", "#003B5C", "Ocean"),
            new ColorMatch(5, "Lagoon", "#00A6A6", "Ocean"),
        ],
    };

    [Fact]
    public async Task Find_colors_trims_the_query_and_caps_the_results()
    {
        var tools = new ColorTools(_catalog);

        var matches = await tools.FindColorsAsync("  sea ", TestContext.Current.CancellationToken);

        Assert.Equal("sea", _catalog.LastQuery);
        Assert.Equal(ColorTools.MaxResults, _catalog.LastLimit);
        Assert.Equal("Deep Sea", Assert.Single(matches).Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Find_colors_rejects_an_empty_query(string query)
    {
        var tools = new ColorTools(_catalog);

        await Assert.ThrowsAsync<McpException>(
            () => tools.FindColorsAsync(query, TestContext.Current.CancellationToken));
    }

    private sealed class FakeColorCatalog : IColorCatalog
    {
        public IReadOnlyList<ColorMatch> Colors { get; init; } = [];

        public string? LastQuery { get; private set; }

        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<ColorMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
        {
            LastQuery = text;
            LastLimit = limit;
            IReadOnlyList<ColorMatch> matches =
                [.. Colors.Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(limit)];
            return Task.FromResult(matches);
        }
    }
}
