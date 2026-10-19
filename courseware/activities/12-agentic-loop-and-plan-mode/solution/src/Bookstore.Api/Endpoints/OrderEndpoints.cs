using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("/", PlaceOrderAsync);
        orders.MapGet("/{id:int}", GetOrderAsync).WithName("GetOrder");

        return app;
    }

    private static async Task<Results<CreatedAtRoute<OrderDto>, ValidationProblem, ProblemHttpResult>> PlaceOrderAsync(
        OrderRequest request,
        BookstoreDbContext db,
        PriceCalculator calculator,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var duplicates = request.Lines.GroupBy(l => l.BookId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            return LinesProblem($"Each book can appear on only one line; repeated: {string.Join(", ", duplicates)}.");
        }

        // Tracked, because placing the order changes each book's stock.
        var ids = request.Lines.Select(l => l.BookId).ToList();
        var books = await db.Books.Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

        var missing = ids.Where(id => !books.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return LinesProblem($"Unknown book id(s): {string.Join(", ", missing)}.");
        }

        Order order;
        try
        {
            var lines = request.Lines.Select(l => new QuoteLine(books[l.BookId], l.Quantity)).ToList();
            order = Order.Place(lines, request.CouponCode, calculator, clock.GetUtcNow());
        }
        catch (ArgumentException ex) when (ex.ParamName == "couponCode")
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CouponCode)] = [ex.Message],
            });
        }
        catch (InsufficientStockException ex)
        {
            return OutOfStock(ex.Message);
        }

        db.Orders.Add(order);

        try
        {
            // One SaveChanges: the order and every stock change commit together, or not at all.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OutOfStock("Stock changed while this order was being placed. Check availability and try again.");
        }

        return TypedResults.CreatedAtRoute(order.ToDto(), "GetOrder", new { id = order.Id });
    }

    private static async Task<Results<Ok<OrderDto>, NotFound>> GetOrderAsync(
        int id, BookstoreDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Lines).ThenInclude(l => l.Book)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order.ToDto());
    }

    private static ValidationProblem LinesProblem(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(OrderRequest.Lines)] = [message],
        });

    private static ProblemHttpResult OutOfStock(string detail) =>
        TypedResults.Problem(title: "Not enough stock", detail: detail, statusCode: StatusCodes.Status409Conflict);
}
