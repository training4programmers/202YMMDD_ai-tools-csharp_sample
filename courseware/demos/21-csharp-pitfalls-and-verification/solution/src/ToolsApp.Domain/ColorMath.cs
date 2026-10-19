using System.Globalization;
using System.Text.RegularExpressions;

namespace ToolsApp.Domain;

/// <summary>Conversions between hex, RGB, and HSL, plus WCAG contrast checks.</summary>
public static partial class ColorMath
{
    /// <summary>
    /// Parses <c>#RGB</c> or <c>#RRGGBB</c> (the <c>#</c> is optional, case is ignored).
    /// </summary>
    public static bool TryParseHex(string? hex, out Rgb rgb)
    {
        rgb = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var digits = hex.Trim().TrimStart('#');

        // Expand shorthand: "F0A" means "FF00AA".
        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(c => new string(c, 2)));
        }

        if (digits.Length != 6 ||
            !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        rgb = new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    /// <summary>Parses a hex color, throwing <see cref="FormatException"/> when it is malformed.</summary>
    public static Rgb ParseHex(string hex) =>
        TryParseHex(hex, out var rgb) ? rgb : throw new FormatException($"'{hex}' is not a #RRGGBB color.");

    /// <summary>Formats as upper-case <c>#RRGGBB</c>.</summary>
    public static string ToHex(Rgb rgb) => $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";

    /// <summary>Normalizes any accepted hex form to upper-case <c>#RRGGBB</c>.</summary>
    public static string NormalizeHex(string hex) => ToHex(ParseHex(hex));

    public static Hsl ToHsl(Rgb rgb)
    {
        var r = rgb.R / 255.0;
        var g = rgb.G / 255.0;
        var b = rgb.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;

        if (max == min)
        {
            return new Hsl(0, 0, lightness); // a shade of gray has no hue
        }

        var delta = max - min;
        var saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);

        var hue = max switch
        {
            _ when max == r => ((g - b) / delta) + (g < b ? 6 : 0),
            _ when max == g => ((b - r) / delta) + 2,
            _ => ((r - g) / delta) + 4,
        };

        return new Hsl(hue * 60, saturation, lightness);
    }

    /// <summary>
    /// Converts HSL back to RGB, rounding each channel to the nearest byte. The inverse of
    /// <see cref="ToHsl"/>: every RGB color survives the round trip unchanged.
    /// </summary>
    public static Rgb FromHsl(Hsl hsl)
    {
        var hue = (((hsl.Hue % 360) + 360) % 360) / 360;
        var saturation = Math.Clamp(hsl.Saturation, 0, 1);
        var lightness = Math.Clamp(hsl.Lightness, 0, 1);

        if (saturation == 0)
        {
            var gray = ToChannel(lightness);
            return new Rgb(gray, gray, gray);
        }

        var q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - (lightness * saturation);
        var p = (2 * lightness) - q;

        return new Rgb(
            ToChannel(HueToRgb(p, q, hue + (1.0 / 3))),
            ToChannel(HueToRgb(p, q, hue)),
            ToChannel(HueToRgb(p, q, hue - (1.0 / 3))));

        static double HueToRgb(double p, double q, double t)
        {
            t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
            return t switch
            {
                < 1.0 / 6 => p + ((q - p) * 6 * t),
                < 1.0 / 2 => q,
                < 2.0 / 3 => p + ((q - p) * ((2.0 / 3) - t) * 6),
                _ => p,
            };
        }

        static byte ToChannel(double value) => (byte)Math.Round(value * 255, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Parses a CSS <c>hsl()</c> color such as <c>hsl(38, 78%, 81%)</c> or <c>hsl(38deg 78% 81%)</c>.
    /// Numbers always use <c>.</c> as the decimal separator, whatever the current culture.
    /// </summary>
    public static bool TryParseHsl(string? value, out Hsl hsl)
    {
        hsl = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = CssHsl().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        var hue = double.Parse(match.Groups["h"].ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture);
        var saturation = double.Parse(match.Groups["s"].ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture);
        var lightness = double.Parse(match.Groups["l"].ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture);

        if (saturation > 100 || lightness > 100)
        {
            return false;
        }

        hsl = new Hsl(hue, saturation / 100, lightness / 100);
        return true;
    }

    // hsl(H, S%, L%) or hsl(H S% L%), with an optional "deg" unit on the hue. [0-9], not \d: \d also
    // matches non-ASCII digits, which double.Parse with the invariant culture would then reject.
    [GeneratedRegex(
        @"^hsl\(\s*(?<h>[+-]?[0-9]+(\.[0-9]+)?)(deg)?\s*(,\s*|\s+)(?<s>[0-9]+(\.[0-9]+)?)%\s*(,\s*|\s+)(?<l>[0-9]+(\.[0-9]+)?)%\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssHsl();

    /// <summary>WCAG 2.x relative luminance, from 0 (black) to 1 (white).</summary>
    public static double RelativeLuminance(Rgb rgb)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(rgb.R)) + (0.7152 * Channel(rgb.G)) + (0.0722 * Channel(rgb.B));
    }

    /// <summary>The WCAG contrast ratio between two colors, from 1 (identical) to 21 (black on white).</summary>
    public static double ContrastRatio(Rgb first, Rgb second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        var (lighter, darker) = a >= b ? (a, b) : (b, a);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static WcagRating Rate(double contrastRatio) => contrastRatio switch
    {
        >= 7.0 => WcagRating.AAA,
        >= 4.5 => WcagRating.AA,
        >= 3.0 => WcagRating.AALarge,
        _ => WcagRating.Fail,
    };
}
