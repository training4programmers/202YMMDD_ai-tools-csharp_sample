using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;

namespace ToolsApp.Api.Endpoints;

public static class ColorSearchEndpoints
{
    public static IEndpointRouteBuilder MapColorSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/colors/search", SearchColorsAsync).WithTags("Colors");
        return app;
    }

    // GET /api/colors/search?q=sea matches names or hex values, ignoring case.
    private static async Task<Results<Ok<List<ColorDto>>, ValidationProblem>> SearchColorsAsync(
        string? q, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["q"] = ["q is required."],
            });
        }

        var text = q.Trim();

        // Palettes are small, so filter in memory where an ordinal, culture-independent comparison is easy.
        var colors = await db.Colors.Include(c => c.Palette).AsNoTracking().ToListAsync(cancellationToken);
        var matches = colors
            .Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                        c.Hex.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => c.ToDto())
            .ToList();

        return TypedResults.Ok(matches);
    }
}
