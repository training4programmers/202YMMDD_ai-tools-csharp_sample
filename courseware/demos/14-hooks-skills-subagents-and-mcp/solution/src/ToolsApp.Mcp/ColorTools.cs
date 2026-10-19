using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ToolsApp.Domain;

namespace ToolsApp.Mcp;

/// <summary>The result of a contrast check, shaped for a model to read.</summary>
public sealed record ContrastCheck(string Foreground, string Background, double Ratio, string Rating, bool PassesBodyText);

/// <summary>
/// The tools this server offers. The model reads each <see cref="DescriptionAttribute"/> to decide when to call
/// a tool and what to pass, so write them for the model, not for a human reader.
/// </summary>
[McpServerToolType]
public sealed class ColorTools(IColorCatalog catalog)
{
    public const int MaxResults = 25;

    [McpServerTool(Name = "find_colors", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds stored ToolsApp colors whose name, hex value, or palette name contains the query text " +
                 "(case-insensitive). Returns at most 25 matches with id, name, #RRGGBB hex, and palette.")]
    public async Task<IReadOnlyList<ColorMatch>> FindColorsAsync(
        [Description("Text to look for, for example 'sea', '#00A6', or 'Sunset'.")] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("query must not be empty.");
        }

        return await catalog.SearchAsync(query.Trim(), MaxResults, cancellationToken);
    }

    [McpServerTool(Name = "check_contrast", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Calculates the WCAG 2.x contrast ratio between a text (foreground) color and a background color, " +
                 "and rates it: AAA (7:1 or more), AA (4.5:1, body text), AALarge (3:1, large text only), or Fail. " +
                 "Does not read the database, so it works for any two colors.")]
    public static ContrastCheck CheckContrast(
        [Description("Text color as #RGB or #RRGGBB, for example '#003B5C'.")] string foregroundHex,
        [Description("Background color as #RGB or #RRGGBB, for example '#FFFFFF'.")] string backgroundHex)
    {
        var foreground = ParseOrExplain(foregroundHex, nameof(foregroundHex));
        var background = ParseOrExplain(backgroundHex, nameof(backgroundHex));

        var ratio = ColorMath.ContrastRatio(foreground, background);
        var rating = ColorMath.Rate(ratio);

        // Rate the exact ratio, and truncate (never round) the displayed one, so a near miss can't look like a
        // pass: Lagoon on white is 2.998:1, which shows as 2.99 and fails, not as 3 and "AALarge".
        return new ContrastCheck(
            ColorMath.ToHex(foreground),
            ColorMath.ToHex(background),
            Math.Floor(ratio * 100) / 100,
            rating.ToString(),
            rating >= WcagRating.AA);
    }

    private static Rgb ParseOrExplain(string hex, string parameterName) =>
        ColorMath.TryParseHex(hex, out var rgb)
            ? rgb
            : throw new McpException($"{parameterName} must look like #RGB or #RRGGBB, but was '{hex}'.");
}
