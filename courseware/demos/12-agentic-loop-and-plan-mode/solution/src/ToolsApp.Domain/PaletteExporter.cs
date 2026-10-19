using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ToolsApp.Domain;

/// <summary>The file formats a palette can be exported to.</summary>
public enum PaletteExportFormat
{
    /// <summary>CSS custom properties in a <c>:root</c> block.</summary>
    Css,

    /// <summary>The palette name and colors as JSON.</summary>
    Json,

    /// <summary>A GIMP palette (version 2 <c>.gpl</c> format).</summary>
    Gpl,
}

/// <summary>An exported palette: the text to send, its media type, and the download file name.</summary>
public sealed record PaletteExport(string FileName, string ContentType, string Content);

/// <summary>Turns a <see cref="Palette"/> into CSS, JSON, or GIMP palette text.</summary>
public static class PaletteExporter
{
    private const int MaxGplColumns = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n", // the same output on every OS
    };

    // A dictionary rather than Enum.TryParse, which would also accept "1" or "99" as a format.
    private static readonly Dictionary<string, PaletteExportFormat> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["css"] = PaletteExportFormat.Css,
        ["json"] = PaletteExportFormat.Json,
        ["gpl"] = PaletteExportFormat.Gpl,
    };

    /// <summary>The accepted <c>format</c> values, for error messages.</summary>
    public static IReadOnlyCollection<string> FormatNames => Formats.Keys;

    /// <summary>Reads a format name such as <c>css</c> (case is ignored).</summary>
    public static bool TryParseFormat(string? value, out PaletteExportFormat format) =>
        Formats.TryGetValue(value ?? "", out format);

    public static PaletteExport Export(Palette palette, PaletteExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(palette);

        var slug = Slugify(palette.Name);
        return format switch
        {
            PaletteExportFormat.Css => new($"{slug}.css", "text/css; charset=utf-8", ToCss(palette)),
            PaletteExportFormat.Json => new($"{slug}.json", "application/json; charset=utf-8", ToJson(palette)),
            PaletteExportFormat.Gpl => new($"{slug}.gpl", "application/x-gimp-palette", ToGpl(palette)),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format."),
        };
    }

    /// <summary>One <c>--palette-color: #RRGGBB;</c> custom property per color, inside <c>:root</c>.</summary>
    public static string ToCss(Palette palette)
    {
        var prefix = Slugify(palette.Name);
        var css = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"/* {palette.Name} palette exported from ToolsApp */\n")
            .Append(":root {\n");

        foreach (var color in InExportOrder(palette))
        {
            css.Append(CultureInfo.InvariantCulture, $"  --{prefix}-{Slugify(color.Name)}: {color.Hex};\n");
        }

        return css.Append("}\n").ToString();
    }

    /// <summary>The palette name and each color's name, hex, and RGB channels, as indented camelCase JSON.</summary>
    public static string ToJson(Palette palette)
    {
        var document = new
        {
            palette.Name,
            Colors = InExportOrder(palette).Select(c => new { c.Name, c.Hex, Rgb = c.ToRgb() }),
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>
    /// A GIMP palette (https://developer.gimp.org/core/standards/gpl). The format requires LF line endings,
    /// so this never uses <see cref="Environment.NewLine"/>.
    /// </summary>
    public static string ToGpl(Palette palette)
    {
        var colors = InExportOrder(palette).ToList();
        var gpl = new StringBuilder()
            .Append("GIMP Palette\n")
            .Append(CultureInfo.InvariantCulture, $"Name: {palette.Name}\n")
            .Append(CultureInfo.InvariantCulture, $"Columns: {Math.Clamp(colors.Count, 1, MaxGplColumns)}\n")
            .Append("#\n");

        foreach (var color in colors)
        {
            var rgb = color.ToRgb();
            gpl.Append(CultureInfo.InvariantCulture, $"{rgb.R,3} {rgb.G,3} {rgb.B,3}\t{color.Name}\n");
        }

        return gpl.ToString();
    }

    /// <summary>
    /// Lower-cases <paramref name="name"/> and replaces each run of characters other than <c>a-z</c> and
    /// <c>0-9</c> with one hyphen: <c>"Deep Sea"</c> becomes <c>deep-sea</c>.
    /// </summary>
    public static string Slugify(string name)
    {
        var slug = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-');
    }

    private static IEnumerable<Color> InExportOrder(Palette palette) => palette.Colors.OrderBy(c => c.Id);
}
