using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using ToolsApp.Api.Contracts;
using ToolsApp.Api.Data;
using ToolsApp.Domain;

namespace ToolsApp.Api.Endpoints;

/// <summary>
/// Export a palette as CSS custom properties, and import colors into a palette from CSV.
/// </summary>
public static partial class PaletteTransferEndpoints
{
    public static IEndpointRouteBuilder MapPaletteTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var palettes = app.MapGroup("/api/palettes").WithTags("Palettes");

        palettes.MapGet("/{id:int}/export.css", ExportCssAsync);
        palettes.MapPost("/{id:int}/import", ImportCsvAsync);

        return app;
    }

    // GET /api/palettes/2/export.css
    // Returns the palette as CSS custom properties, with an ETag so browsers can cache it.
    private static async Task<Results<ContentHttpResult, StatusCodeHttpResult, NotFound>> ExportCssAsync(
        int id, HttpContext http, ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        var palette = await db.Palettes.Include(p => p.Colors).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (palette is null)
        {
            return TypedResults.NotFound();
        }

        var css = PaletteCss.Render(palette);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(css)))}\"");

        if (http.Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        http.Response.GetTypedHeaders().ETag = etag;
        return TypedResults.Text(css, "text/css");
    }

    // POST /api/palettes/2/import
    // Body (text/csv): one "name,color" pair per line; see PaletteCsv for the accepted formats.
    // The whole file is validated before anything is saved.
    private static async Task<Results<Ok<ImportResultDto>, NotFound, ValidationProblem>> ImportCsvAsync(
        int id, HttpRequest request, ToolsAppDbContext db, ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var palette = await db.Palettes.FindAsync([id], cancellationToken);
        if (palette is null)
        {
            return TypedResults.NotFound();
        }

        using var reader = new StreamReader(request.Body);
        var result = PaletteCsv.Parse(await reader.ReadToEndAsync(cancellationToken));

        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csv"] = [.. result.Errors],
            });
        }

        foreach (var color in result.Colors)
        {
            palette.Colors.Add(new Color { Name = color.Name, Hex = color.Hex });
        }

        await db.SaveChangesAsync(cancellationToken);

        LogImported(logger, result.Colors.Count, id);
        return TypedResults.Ok(new ImportResultDto(result.Colors.Count));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Count} colors into palette {PaletteId}")]
    private static partial void LogImported(ILogger logger, int count, int paletteId);
}
