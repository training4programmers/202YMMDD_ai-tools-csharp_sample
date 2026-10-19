namespace ToolsApp.Domain;

/// <summary>A color as 8-bit red, green, and blue channels.</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// A color as hue (0–360 degrees), saturation (0–1), and lightness (0–1).
/// </summary>
public readonly record struct Hsl(double Hue, double Saturation, double Lightness);

/// <summary>How a foreground/background pair scores against the WCAG 2.x contrast thresholds.</summary>
public enum WcagRating
{
    /// <summary>Below 3:1. Not readable for text.</summary>
    Fail,

    /// <summary>At least 3:1. Acceptable for large text (18pt, or 14pt bold) only.</summary>
    AALarge,

    /// <summary>At least 4.5:1. Acceptable for body text.</summary>
    AA,

    /// <summary>At least 7:1. Enhanced contrast.</summary>
    AAA,
}
