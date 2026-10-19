using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;
using ToolsApp.Domain;

namespace ToolsApp.Api.Endpoints;

public static class PaletteEndpoints
{
    public static IEndpointRouteBuilder MapPaletteEndpoints(this IEndpointRouteBuilder app)
    {
        var palettes = app.MapGroup("/api/palettes").WithTags("Palettes");

        palettes.MapGet("/", GetPalettesAsync);
        palettes.MapGet("/{id:int}/colors", GetPaletteColorsAsync);
        palettes.MapGet("/{id:int}/export", ExportPaletteAsync);

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

    // GET /api/palettes/2/export?format=gpl downloads the palette as a file.
    private static async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> ExportPaletteAsync(
        int id, string? format, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        if (!PaletteExporter.TryParseFormat(format, out var exportFormat))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["format"] = [$"format must be one of: {string.Join(", ", PaletteExporter.FormatNames)}."],
            });
        }

        var palette = await db.Palettes.Include(p => p.Colors).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (palette is null)
        {
            return TypedResults.NotFound();
        }

        var export = PaletteExporter.Export(palette, exportFormat);

        // File results add Content-Disposition: attachment with the download name.
        return TypedResults.File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
    }
}
