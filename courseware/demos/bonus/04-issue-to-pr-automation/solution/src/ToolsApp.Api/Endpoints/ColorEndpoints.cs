using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;
using ToolsApp.Domain;

namespace ToolsApp.Api.Endpoints;

public static class ColorEndpoints
{
    public static IEndpointRouteBuilder MapColorEndpoints(this IEndpointRouteBuilder app)
    {
        var colors = app.MapGroup("/api/colors").WithTags("Colors");

        colors.MapGet("/", GetColorsAsync);
        colors.MapGet("/{id:int}", GetColorAsync).WithName("GetColor");
        colors.MapGet("/{id:int}/contrast", GetContrastAsync);
        colors.MapGet("/{id:int}/tints", GetTintsAsync);
        colors.MapPost("/", CreateColorAsync);
        colors.MapPut("/{id:int}", UpdateColorAsync);
        colors.MapDelete("/{id:int}", DeleteColorAsync);

        return app;
    }

    // GET /api/colors?paletteId=2
    private static async Task<Ok<List<ColorDto>>> GetColorsAsync(
        ToolsAppDbContext db, int? paletteId, CancellationToken cancellationToken)
    {
        var query = db.Colors.Include(c => c.Palette).AsNoTracking();

        if (paletteId is not null)
        {
            query = query.Where(c => c.PaletteId == paletteId);
        }

        var colors = await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok(colors.Select(c => c.ToDto()).ToList());
    }

    private static async Task<Results<Ok<ColorDto>, NotFound>> GetColorAsync(
        int id, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var color = await db.Colors.Include(c => c.Palette).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return color is null ? TypedResults.NotFound() : TypedResults.Ok(color.ToDto());
    }

    // GET /api/colors/3/contrast?against=%23FFFFFF
    private static async Task<Results<Ok<ContrastDto>, NotFound, ValidationProblem>> GetContrastAsync(
        int id, string? against, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var background = string.IsNullOrWhiteSpace(against) ? "#FFFFFF" : against;
        if (!ColorMath.TryParseHex(background, out var backgroundRgb))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["against"] = ["against must look like #RGB or #RRGGBB."],
            });
        }

        var color = await db.Colors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (color is null)
        {
            return TypedResults.NotFound();
        }

        var ratio = ColorMath.ContrastRatio(color.ToRgb(), backgroundRgb);
        return TypedResults.Ok(new ContrastDto(
            color.Hex, ColorMath.ToHex(backgroundRgb), Math.Round(ratio, 2), ColorMath.Rate(ratio)));
    }

    // GET /api/colors/4/tints?count=3
    private static async Task<Results<Ok<TintsDto>, NotFound, ValidationProblem>> GetTintsAsync(
        int id, int? count, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        const int DefaultCount = 5;
        const int MaxCount = 10;

        var tintCount = count ?? DefaultCount;
        if (tintCount is < 1 or > MaxCount)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["count"] = [$"count must be between 1 and {MaxCount}."],
            });
        }

        var color = await db.Colors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (color is null)
        {
            return TypedResults.NotFound();
        }

        var tints = ColorMath.Tints(color.ToRgb(), tintCount).Select(ColorMath.ToHex).ToList();
        return TypedResults.Ok(new TintsDto(color.Id, color.Hex, tints));
    }

    private static async Task<Results<CreatedAtRoute<ColorDto>, ValidationProblem>> CreateColorAsync(
        ColorRequest request, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Palettes.AnyAsync(p => p.Id == request.PaletteId, cancellationToken))
        {
            return UnknownPalette(request.PaletteId);
        }

        var color = new Color
        {
            Name = request.Name,
            Hex = ColorMath.NormalizeHex(request.Hex),
            PaletteId = request.PaletteId,
        };

        db.Colors.Add(color);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(color).Reference(c => c.Palette).LoadAsync(cancellationToken);

        return TypedResults.CreatedAtRoute(color.ToDto(), "GetColor", new { id = color.Id });
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateColorAsync(
        int id, ColorRequest request, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var color = await db.Colors.FindAsync([id], cancellationToken);
        if (color is null)
        {
            return TypedResults.NotFound();
        }

        if (!await db.Palettes.AnyAsync(p => p.Id == request.PaletteId, cancellationToken))
        {
            return UnknownPalette(request.PaletteId);
        }

        color.Name = request.Name;
        color.Hex = ColorMath.NormalizeHex(request.Hex);
        color.PaletteId = request.PaletteId;

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteColorAsync(
        int id, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var deleted = await db.Colors.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static ValidationProblem UnknownPalette(int paletteId) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(ColorRequest.PaletteId)] = [$"Palette {paletteId} does not exist."],
        });
}
