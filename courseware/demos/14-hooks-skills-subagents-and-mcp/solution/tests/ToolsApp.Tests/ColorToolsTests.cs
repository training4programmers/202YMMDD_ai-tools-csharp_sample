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

    [Fact]
    public void Check_contrast_rates_black_on_white_as_AAA()
    {
        var result = ColorTools.CheckContrast("#000000", "#FFFFFF");

        Assert.Equal(new ContrastCheck("#000000", "#FFFFFF", 21, "AAA", PassesBodyText: true), result);
    }

    [Fact]
    public void Check_contrast_normalizes_short_and_lower_case_hex()
    {
        var result = ColorTools.CheckContrast("fff", "#003b5c");

        Assert.Equal("#FFFFFF", result.Foreground);
        Assert.Equal("#003B5C", result.Background);
    }

    [Fact]
    public void Check_contrast_fails_mid_gray_on_white_for_body_text()
    {
        // #777777 on white is 4.478:1, a classic near miss for AA.
        var result = ColorTools.CheckContrast("#777777", "#FFFFFF");

        Assert.Equal(4.47, result.Ratio);
        Assert.Equal("AALarge", result.Rating);
        Assert.False(result.PassesBodyText);
    }

    [Fact]
    public void Check_contrast_never_rounds_a_failing_ratio_up_to_a_threshold()
    {
        // Lagoon on white is 2.998:1. Rounded, that would read "3", the AALarge threshold, next to "Fail".
        var result = ColorTools.CheckContrast("#00A6A6", "#FFFFFF");

        Assert.Equal(2.99, result.Ratio);
        Assert.Equal("Fail", result.Rating);
    }

    [Theory]
    [InlineData("#GG0000", "#FFFFFF", "foregroundHex")]
    [InlineData("#000000", "white", "backgroundHex")]
    public void Check_contrast_names_the_bad_argument(string foreground, string background, string parameter)
    {
        var error = Assert.Throws<McpException>(() => ColorTools.CheckContrast(foreground, background));

        Assert.StartsWith(parameter, error.Message, StringComparison.Ordinal);
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
