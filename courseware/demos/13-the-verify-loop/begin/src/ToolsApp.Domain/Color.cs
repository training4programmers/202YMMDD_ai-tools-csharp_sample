namespace ToolsApp.Domain;

/// <summary>A named color in a palette, stored as a <c>#RRGGBB</c> hex string.</summary>
public sealed class Color
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>The color as <c>#RRGGBB</c> (upper-case, with the leading <c>#</c>).</summary>
    public required string Hex { get; set; }

    public int PaletteId { get; set; }

    public Palette? Palette { get; set; }

    /// <summary>The color's red, green, and blue channels.</summary>
    public Rgb ToRgb() => ColorMath.ParseHex(Hex);
}
