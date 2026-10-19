using System.Globalization;

namespace ToolsApp.Domain;

/// <summary>Conversions between hex, RGB, and HSL, plus WCAG contrast checks.</summary>
public static class ColorMath
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

    /// <summary>
    /// Returns <paramref name="count"/> evenly spaced tints of <paramref name="rgb"/>, darkest first. Tint
    /// <c>k</c> moves each channel <c>k / (count + 1)</c> of the way to white; neither the base color nor
    /// white is included.
    /// </summary>
    public static IReadOnlyList<Rgb> Tints(Rgb rgb, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        return [.. Enumerable.Range(1, count).Select(k => new Rgb(
            TowardWhite(rgb.R, k, count),
            TowardWhite(rgb.G, k, count),
            TowardWhite(rgb.B, k, count)))];

        // Decimal, not double, so a midpoint such as 127.5 rounds the same way every time.
        static byte TowardWhite(byte channel, int step, int count) =>
            (byte)Math.Round(channel + ((255m - channel) * step / (count + 1)), MidpointRounding.AwayFromZero);
    }
}
