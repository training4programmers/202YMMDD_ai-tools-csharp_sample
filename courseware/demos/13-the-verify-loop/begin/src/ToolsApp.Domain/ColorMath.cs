using System.Globalization;

namespace ToolsApp.Domain;

/// <summary>Conversions between hex, RGB, and HSL, plus WCAG contrast checks.</summary>
public static class ColorMath
{
    // Minimum contrast ratio for each rating, best first.
    private static readonly (double Minimum, WcagRating Rating)[] Thresholds =
    [
        (7.0, WcagRating.AAA),
        (4.5, WcagRating.AA),
        (3.0, WcagRating.AALarge),
    ];

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

        scoped var digits = hex.AsSpan().Trim().TrimStart('#');

        // Expand shorthand without allocating: "F0A" means "FF00AA".
        Span<char> expanded = stackalloc char[6];
        if (digits.Length == 3)
        {
            for (var i = 0; i < 3; i++)
            {
                expanded[i] = digits[i];
                expanded[i + 3] = digits[i];
            }

            digits = expanded;
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
    /// Formats as a CSS Color 4 <c>hsl()</c> value with whole numbers, for example <c>hsl(180 100% 33%)</c>.
    /// </summary>
    public static string ToCssHsl(Hsl hsl) =>
        string.Format("hsl({0:0} {1}% {2}%)", hsl.Hue, (int)(hsl.Saturation * 100), (int)(hsl.Lightness * 100));

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

    public static WcagRating Rate(double contrastRatio)
    {
        foreach (var (minimum, rating) in Thresholds)
        {
            if (contrastRatio > minimum)
            {
                return rating;
            }
        }

        return WcagRating.Fail;
    }
}
