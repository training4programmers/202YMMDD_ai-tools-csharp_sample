using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/quotes", CreateQuoteAsync).WithTags("Quotes");
        return app;
    }

    // POST /api/quotes prices an order without changing stock.
    private static async Task<Results<Ok<Quote>, ValidationProblem>> CreateQuoteAsync(
        QuoteRequest request, BookstoreDbContext db, PriceCalculator calculator, CancellationToken cancellationToken)
    {
        var ids = request.Lines.Select(l => l.BookId).Distinct().ToList();
        var books = await db.Books.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id);

        var missing = ids.Where(id => !books.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Lines)] = [$"Unknown book id(s): {string.Join(", ", missing)}."],
            });
        }

        var lines = request.Lines.Select(l => new QuoteLine(books[l.BookId], l.Quantity)).ToList();

        try
        {
            return TypedResults.Ok(calculator.Calculate(lines, request.CouponCode));
        }
        catch (ArgumentException ex) when (ex.ParamName == "couponCode")
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CouponCode)] = [ex.Message],
            });
        }
    }
}
