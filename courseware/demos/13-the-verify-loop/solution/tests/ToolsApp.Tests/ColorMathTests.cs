using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class ColorMathTests
{
    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("00a6a6", 0, 166, 166)]
    [InlineData("#F0A", 255, 0, 170)]
    public void TryParseHex_reads_long_and_short_forms(string hex, byte r, byte g, byte b)
    {
        Assert.True(ColorMath.TryParseHex(hex, out var rgb));
        Assert.Equal(new Rgb(r, g, b), rgb);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#GG0000")]
    [InlineData("#12345")]
    public void TryParseHex_rejects_malformed_values(string? hex) =>
        Assert.False(ColorMath.TryParseHex(hex, out _));

    [Fact]
    public void ToHex_formats_upper_case_with_hash() =>
        Assert.Equal("#00A6A6", ColorMath.ToHex(new Rgb(0, 166, 166)));

    [Fact]
    public void ToHsl_converts_pure_red()
    {
        var hsl = ColorMath.ToHsl(new Rgb(255, 0, 0));

        Assert.Equal(0, hsl.Hue, precision: 3);
        Assert.Equal(1, hsl.Saturation, precision: 3);
        Assert.Equal(0.5, hsl.Lightness, precision: 3);
    }

    [Fact]
    public void ContrastRatio_of_black_on_white_is_21() =>
        Assert.Equal(21, ColorMath.ContrastRatio(new Rgb(0, 0, 0), new Rgb(255, 255, 255)), precision: 3);

    [Theory]
    [InlineData(21.0, WcagRating.AAA)]
    [InlineData(4.5, WcagRating.AA)]
    [InlineData(3.2, WcagRating.AALarge)]
    [InlineData(1.0, WcagRating.Fail)]
    public void Rate_maps_ratios_to_wcag_levels(double ratio, WcagRating expected) =>
        Assert.Equal(expected, ColorMath.Rate(ratio));

    [Theory]
    [InlineData("#FF0000", "hsl(0 100% 50%)")]
    [InlineData("#00A6A6", "hsl(180 100% 33%)")] // lightness 32.5% rounds up
    [InlineData("#FFB000", "hsl(41 100% 50%)")]
    public void ToCssHsl_rounds_to_whole_numbers(string hex, string expected) =>
        Assert.Equal(expected, ColorMath.ToCssHsl(ColorMath.ToHsl(ColorMath.ParseHex(hex))));
}
