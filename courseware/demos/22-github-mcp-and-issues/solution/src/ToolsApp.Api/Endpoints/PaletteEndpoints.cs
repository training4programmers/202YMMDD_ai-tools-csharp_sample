using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;

namespace ToolsApp.Api.Endpoints;

public static class PaletteEndpoints
{
    // SQLite's result code for a constraint violation, including a unique index.
    private const int SqliteConstraintError = 19;

    public static IEndpointRouteBuilder MapPaletteEndpoints(this IEndpointRouteBuilder app)
    {
        var palettes = app.MapGroup("/api/palettes").WithTags("Palettes");

        palettes.MapGet("/", GetPalettesAsync);
        palettes.MapGet("/{id:int}/colors", GetPaletteColorsAsync);
        palettes.MapPatch("/{id:int}", RenamePaletteAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Ok<List<PaletteDto>>> GetPalettesAsync(
        ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var palettes = await db.Palettes
            .OrderBy(p => p.Name)
            .Select(p => new PaletteDto(p.Id, p.Name, p.Colors.Count))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(palettes);
    }

    private static async Task<Results<Ok<List<ColorDto>>, NotFound>> GetPaletteColorsAsync(
        int id, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var palette = await db.Palettes.Include(p => p.Colors).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (palette is null)
        {
            return TypedResults.NotFound();
        }

        var colors = palette.Colors
            .OrderBy(c => c.Id)
            .Select(c => c.ToDto() with { PaletteName = palette.Name })
            .ToList();

        return TypedResults.Ok(colors);
    }

    // PATCH /api/palettes/2 { "name": "Ocean Blues" }
    private static async Task<Results<Ok<PaletteDto>, NotFound, ProblemHttpResult>> RenamePaletteAsync(
        int id, PaletteRenameRequest request, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var palette = await db.Palettes.FindAsync([id], cancellationToken);
        if (palette is null)
        {
            return TypedResults.NotFound();
        }

        var name = request.Name.Trim();

        // The column's NOCASE collation makes this comparison case-insensitive in SQL. Excluding this
        // palette lets it change the casing of its own name.
        if (await db.Palettes.AnyAsync(p => p.Id != id && p.Name == name, cancellationToken))
        {
            return DuplicateName(name);
        }

        palette.Name = name;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintError })
        {
            // Another request took the name between the check and the save; the unique index caught it.
            return DuplicateName(name);
        }

        var colorCount = await db.Colors.CountAsync(c => c.PaletteId == id, cancellationToken);
        return TypedResults.Ok(new PaletteDto(palette.Id, palette.Name, colorCount));
    }

    private static ProblemHttpResult DuplicateName(string name) =>
        TypedResults.Problem(
            title: "Palette name already in use",
            detail: $"Another palette is already named '{name}' (names are compared ignoring case).",
            statusCode: StatusCodes.Status409Conflict);
}
