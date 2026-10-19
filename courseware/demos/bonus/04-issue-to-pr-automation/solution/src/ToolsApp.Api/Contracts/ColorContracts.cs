using System.ComponentModel.DataAnnotations;
using ToolsApp.Domain;

namespace ToolsApp.Api.Contracts;

/// <summary>A color as the API returns it, with its RGB and HSL forms precomputed.</summary>
public sealed record ColorDto(int Id, string Name, string Hex, Rgb Rgb, Hsl Hsl, int PaletteId, string PaletteName);

/// <summary>The body of a create or update request. Validated before the handler runs.</summary>
public sealed record ColorRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, RegularExpression("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Hex must look like #RGB or #RRGGBB.")]
    public required string Hex { get; init; }

    [Range(1, int.MaxValue)]
    public int PaletteId { get; init; }
}

/// <summary>A palette with a count of its colors.</summary>
public sealed record PaletteDto(int Id, string Name, int ColorCount);

/// <summary>Evenly spaced tints of a stored color, darkest first.</summary>
public sealed record TintsDto(int ColorId, string BaseHex, IReadOnlyList<string> Tints);

/// <summary>The contrast between a stored color and another color.</summary>
public sealed record ContrastDto(string Foreground, string Background, double Ratio, WcagRating Rating);

public static class ColorMappings
{
    public static ColorDto ToDto(this Color color)
    {
        var rgb = color.ToRgb();
        var hsl = ColorMath.ToHsl(rgb);
        var rounded = new Hsl(Math.Round(hsl.Hue, 1), Math.Round(hsl.Saturation, 3), Math.Round(hsl.Lightness, 3));

        return new ColorDto(color.Id, color.Name, color.Hex, rgb, rounded, color.PaletteId, color.Palette?.Name ?? "");
    }
}
