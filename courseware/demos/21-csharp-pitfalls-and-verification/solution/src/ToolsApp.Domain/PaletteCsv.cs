namespace ToolsApp.Domain;

/// <summary>A color read from an import file, already normalized to upper-case <c>#RRGGBB</c>.</summary>
public sealed record ImportedColor(string Name, string Hex);

/// <summary>The colors an import file contains, or the reasons its lines were rejected.</summary>
public sealed record PaletteCsvResult(IReadOnlyList<ImportedColor> Colors, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Reads palette import files: one <c>name,value</c> pair per line, where the value is a hex color
/// (<c>#RGB</c> or <c>#RRGGBB</c>, the <c>#</c> optional) or a CSS <c>hsl()</c> color.
/// </summary>
public static class PaletteCsv
{
    public const int MaxNameLength = 60;

    /// <summary>
    /// Parses the whole file. Blank lines are skipped, and so is a first line whose name field is
    /// <c>name</c> (a header). Any malformed line is reported by line number; nothing is partially valid.
    /// </summary>
    public static PaletteCsvResult Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var colors = new List<ImportedColor>();
        var errors = new List<string>();
        var lines = csv.ReplaceLineEndings("\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // Split at the first comma only: hsl(38, 78%, 81%) has commas of its own.
            var fields = line.Split(',', 2, StringSplitOptions.TrimEntries);
            if (i == 0 && fields[0].Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lineNumber = i + 1;
            if (fields.Length != 2 || fields[0].Length is 0 or > MaxNameLength)
            {
                errors.Add($"Line {lineNumber}: expected 'name,color' with a name of 1 to {MaxNameLength} characters.");
                continue;
            }

            if (!TryParseColor(fields[1], out var rgb))
            {
                errors.Add($"Line {lineNumber}: '{fields[1]}' is not a hex color or an hsl() color.");
                continue;
            }

            colors.Add(new ImportedColor(fields[0], ColorMath.ToHex(rgb)));
        }

        return new PaletteCsvResult(colors, errors);
    }

    private static bool TryParseColor(string value, out Rgb rgb)
    {
        if (ColorMath.TryParseHex(value, out rgb))
        {
            return true;
        }

        if (ColorMath.TryParseHsl(value, out var hsl))
        {
            rgb = ColorMath.FromHsl(hsl);
            return true;
        }

        return false;
    }
}
