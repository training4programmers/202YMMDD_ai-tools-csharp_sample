using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ToolsApp.Mcp;

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
}
