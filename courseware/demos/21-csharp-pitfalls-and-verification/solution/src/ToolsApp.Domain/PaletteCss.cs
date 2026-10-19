using System.Globalization;
using System.Text;

namespace ToolsApp.Domain;

/// <summary>Renders a palette as CSS custom properties.</summary>
public static class PaletteCss
{
    /// <summary>
    /// Renders <c>:root { --palette-color: #RRGGBB; }</c> with one property per color, in id order.
    /// </summary>
    public static string Render(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        var prefix = Slug(palette.Name);
        var css = new StringBuilder(":root {\n");

        // "\n" rather than AppendLine: the output (and its ETag) must not depend on the server's OS.
        foreach (var color in palette.Colors.OrderBy(c => c.Id))
        {
            css.Append(CultureInfo.InvariantCulture, $"  --{prefix}-{Slug(color.Name)}: {color.Hex};\n");
        }

        return css.Append("}\n").ToString();
    }

    /// <summary>
    /// "Deep Sea" becomes "deep-sea". Invariant casing, so "Indigo" is "indigo" on every machine,
    /// including one running in Turkish, where <c>"I".ToLower()</c> is a dotless "ı".
    /// </summary>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return string.Join('-', name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
