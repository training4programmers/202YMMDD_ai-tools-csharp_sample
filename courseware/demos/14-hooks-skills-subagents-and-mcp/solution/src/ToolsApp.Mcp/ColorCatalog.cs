using Microsoft.Data.Sqlite;
using ModelContextProtocol;

namespace ToolsApp.Mcp;

/// <summary>A stored color, as the MCP tools return it.</summary>
public sealed record ColorMatch(int Id, string Name, string Hex, string Palette);

/// <summary>Read-only access to the colors the ToolsApp API stores.</summary>
public interface IColorCatalog
{
    /// <summary>Colors whose name, hex value, or palette name contains <paramref name="text"/> (case-insensitive).</summary>
    Task<IReadOnlyList<ColorMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Queries the API's SQLite file directly. The tables are the ones EF Core creates for
/// <c>ToolsAppDbContext</c>: <c>Colors</c> and <c>Palettes</c>.
/// </summary>
public sealed class SqliteColorCatalog(string connectionString) : IColorCatalog
{
    public async Task<IReadOnlyList<ColorMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Id, c.Name, c.Hex, p.Name
            FROM Colors AS c
            JOIN Palettes AS p ON p.Id = c.PaletteId
            WHERE c.Name LIKE $pattern ESCAPE '\' OR c.Hex LIKE $pattern ESCAPE '\' OR p.Name LIKE $pattern ESCAPE '\'
            ORDER BY p.Name, c.Name
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$pattern", $"%{EscapeLike(text)}%");
        command.Parameters.AddWithValue("$limit", limit);

        var matches = new List<ColorMatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new ColorMatch(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return matches;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (!File.Exists(path))
        {
            // McpException messages reach the model, so say exactly how to fix it.
            throw new McpException(
                $"No ToolsApp database at '{path}'. Run the API once (dotnet run --project src/ToolsApp.Api) to create it.");
        }

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    // '%' and '_' are LIKE wildcards; treat them as literal characters in a search.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
