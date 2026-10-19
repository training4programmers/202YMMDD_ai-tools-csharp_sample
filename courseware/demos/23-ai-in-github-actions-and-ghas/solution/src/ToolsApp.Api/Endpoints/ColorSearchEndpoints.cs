using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;

namespace ToolsApp.Api.Endpoints;

public static class ColorSearchEndpoints
{
    private const string LikeEscape = "\\";

    public static IEndpointRouteBuilder MapColorSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/colors/search", SearchColorsAsync).WithTags("Colors");
        return app;
    }

    // GET /api/colors/search?name=sea
    private static async Task<Ok<List<ColorDto>>> SearchColorsAsync(
        string name, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        // The pattern is sent as a parameter, never spliced into the SQL text. SQLite's LIKE ignores
        // case for ASCII letters, which keeps the endpoint's original case-insensitive behavior.
        var pattern = $"%{EscapeLike(name)}%";

        var colors = await db.Colors
            .Include(c => c.Palette)
            .AsNoTracking()
            .Where(c => EF.Functions.Like(c.Name, pattern, LikeEscape))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(colors.Select(c => c.ToDto()).ToList());
    }

    // LIKE treats % and _ as wildcards; escape them so a search for "50%" matches the text "50%".
    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
