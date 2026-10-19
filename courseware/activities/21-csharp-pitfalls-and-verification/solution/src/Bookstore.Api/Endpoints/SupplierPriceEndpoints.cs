using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

/// <summary>
/// Applies the supplier's weekly price list to the catalog.
/// </summary>
public static partial class SupplierPriceEndpoints
{
    public static IEndpointRouteBuilder MapSupplierPriceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/books/prices/import", ImportPricesAsync).WithTags("Books");
        return app;
    }

    // POST /api/books/prices/import
    // Body (text/csv): see SupplierPriceList for the format. All or nothing: any bad row means a 400
    // listing every bad row, and no price changes.
    private static async Task<Results<Ok<PriceImportResultDto>, ValidationProblem>> ImportPricesAsync(
        HttpRequest request, BookstoreDbContext db, ILogger<Program> logger, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var list = SupplierPriceList.Parse(await reader.ReadToEndAsync(cancellationToken));

        if (!list.IsValid)
        {
            return Invalid(list.Errors);
        }

        // One query for the whole list. The catalog stores ISBNs with hyphens, so compare digits only;
        // EF Core translates Replace to SQL, so the filter still runs in the database.
        var isbns = list.Prices.Select(p => p.Isbn13).ToList();
        var books = await db.Books
            .Where(b => isbns.Contains(b.Isbn.Replace("-", "").Replace(" ", "")))
            .ToDictionaryAsync(b => Isbn.Normalize(b.Isbn), cancellationToken);

        var unknown = list.Prices
            .Where(p => !books.ContainsKey(p.Isbn13))
            .Select(p => $"Line {p.LineNumber}: no book with ISBN {p.Isbn13} is in the catalog.")
            .ToList();

        if (unknown.Count > 0)
        {
            return Invalid(unknown);
        }

        foreach (var price in list.Prices)
        {
            books[price.Isbn13].Price = price.Price;
        }

        await db.SaveChangesAsync(cancellationToken);

        LogPricesUpdated(logger, list.Prices.Count);
        return TypedResults.Ok(new PriceImportResultDto(list.Prices.Count));
    }

    private static ValidationProblem Invalid(IEnumerable<string> errors) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["csv"] = [.. errors],
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Supplier import updated {Count} prices")]
    private static partial void LogPricesUpdated(ILogger logger, int count);
}
