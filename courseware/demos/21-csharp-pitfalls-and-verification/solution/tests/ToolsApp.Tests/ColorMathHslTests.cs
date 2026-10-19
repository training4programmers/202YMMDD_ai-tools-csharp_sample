using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class ColorMathHslTests
{
    [Theory]
    [InlineData(0, 1.0, 0.5, 255, 0, 0)]
    [InlineData(120, 1.0, 0.25, 0, 128, 0)]
    [InlineData(240, 1.0, 0.5, 0, 0, 255)]
    [InlineData(0, 0.0, 0.5, 128, 128, 128)]
    [InlineData(360, 1.0, 0.5, 255, 0, 0)]
    [InlineData(-120, 1.0, 0.5, 0, 0, 255)]
    [InlineData(38, 0.76, 0.81, 243, 216, 170)]
    public void FromHsl_converts_known_colors(double hue, double saturation, double lightness, byte r, byte g, byte b) =>
        Assert.Equal(new Rgb(r, g, b), ColorMath.FromHsl(new Hsl(hue, saturation, lightness)));

    [Fact]
    public void FromHsl_is_the_inverse_of_ToHsl()
    {
        // Every fifth value per channel, plus 255: about 140,000 colors in well under a second.
        int[] channel = [.. Enumerable.Range(0, 52).Select(i => i * 5), 255];

        foreach (var r in channel)
        {
            foreach (var g in channel)
            {
                foreach (var b in channel)
                {
                    var rgb = new Rgb((byte)r, (byte)g, (byte)b);
                    Assert.Equal(rgb, ColorMath.FromHsl(ColorMath.ToHsl(rgb)));
                }
            }
        }
    }

    [Theory]
    [InlineData("hsl(38, 78%, 81%)", 38, 0.78, 0.81)]
    [InlineData("hsl(38deg 78% 81%)", 38, 0.78, 0.81)]
    [InlineData("HSL(7.5, 100%, 67.5%)", 7.5, 1.0, 0.675)]
    [InlineData("  hsl( 200 , 50% , 40% ) ", 200, 0.5, 0.4)]
    public void TryParseHsl_reads_css_hsl(string value, double hue, double saturation, double lightness)
    {
        Assert.True(ColorMath.TryParseHsl(value, out var hsl));
        Assert.Equal(hue, hsl.Hue, precision: 6);
        Assert.Equal(saturation, hsl.Saturation, precision: 6);
        Assert.Equal(lightness, hsl.Lightness, precision: 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hsl(38, 78, 81%)")]
    [InlineData("hsl(38, 120%, 50%)")]
    [InlineData("hsl(38, 78%)")]
    [InlineData("rgb(10, 20, 30)")]
    public void TryParseHsl_rejects_malformed_values(string? value) =>
        Assert.False(ColorMath.TryParseHsl(value, out _));

    [Fact]
    public void TryParseHsl_ignores_the_current_culture()
    {
        // In de-DE, "." is a group separator: double.Parse("7.5") there is 75.
        using var scope = new CultureScope("de-DE");

        Assert.True(ColorMath.TryParseHsl("hsl(7.5, 100%, 67.5%)", out var hsl));
        Assert.Equal(7.5, hsl.Hue, precision: 6);
        Assert.Equal(0.675, hsl.Lightness, precision: 6);
    }
}
