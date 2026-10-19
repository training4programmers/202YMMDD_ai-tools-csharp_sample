using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class NearestColorFinderTests
{
    private readonly NearestColorFinder _finder = new(NamedColors.All);

    [Fact]
    public void NamedColors_has_1000_unique_names_and_valid_hex_values()
    {
        Assert.Equal(1000, NamedColors.All.Count);
        Assert.Equal(1000, NamedColors.All.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(NamedColors.All, c => Assert.True(ColorMath.TryParseHex(c.Hex, out _), c.Hex));
    }

    [Fact]
    public void FindNearest_returns_a_named_color_itself_at_distance_zero()
    {
        var match = _finder.FindNearest("#FDB9B9");

        Assert.Equal(new ColorMatch("Pale Vivid Red", "#FDB9B9", 0), match);
    }

    [Theory]
    [InlineData("#FF0000", "Bright Vivid Red", "#F92424")]
    [InlineData("00a6a6", "Dark Vivid Teal", "#05BDA1")]
    [InlineData("#000", "Deep Muted Red", "#4C2424")]
    public void FindNearest_finds_the_closest_name(string hex, string expectedName, string expectedHex)
    {
        var match = _finder.FindNearest(hex);

        Assert.Equal(expectedName, match.Name);
        Assert.Equal(expectedHex, match.Hex);
    }

    [Fact]
    public void FindNearest_breaks_ties_by_list_order()
    {
        // #000C0C is exactly as far from Deep Muted Green (#264C24) as from Deep Muted Blue (#26244C).
        NamedColor green = new("Deep Muted Green", "#264C24");
        NamedColor blue = new("Deep Muted Blue", "#26244C");

        Assert.Equal("Deep Muted Green", new NearestColorFinder([green, blue]).FindNearest("#000C0C").Name);
        Assert.Equal("Deep Muted Blue", new NearestColorFinder([blue, green]).FindNearest("#000C0C").Name);
        Assert.Equal("Deep Muted Green", _finder.FindNearest("#000C0C").Name);
    }

    [Fact]
    public void FindNearest_for_many_colors_keeps_the_input_order()
    {
        var matches = _finder.FindNearest(["#FDB9B9", "#FF0000", "#FDB9B9"]);

        Assert.Equal(["Pale Vivid Red", "Bright Vivid Red", "Pale Vivid Red"], matches.Select(m => m.Name));
    }

    [Fact]
    public void FindNearest_rejects_malformed_hex() =>
        Assert.Throws<FormatException>(() => _finder.FindNearest("#GG0000"));
}
