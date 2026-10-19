namespace Bookstore.Domain;

/// <summary>A placed order: what was bought, at what price, and the totals charged.</summary>
public sealed class Order
{
    public int Id { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public string? CouponCode { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>
    /// Prices the order, then takes every copy off the shelf. Nothing changes unless every line can be filled.
    /// </summary>
    /// <exception cref="ArgumentException">The coupon code is not recognized.</exception>
    /// <exception cref="InsufficientStockException">At least one book has fewer copies than requested.</exception>
    public static Order Place(
        IReadOnlyList<QuoteLine> lines, string? couponCode, PriceCalculator calculator, DateTimeOffset placedAt)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(calculator);

        // Both checks run before any stock moves, so a bad coupon or a short line leaves every book untouched.
        var quote = calculator.Calculate(lines, couponCode);

        var shortages = lines
            .Where(line => line.Quantity > line.Book.Stock)
            .Select(line => new StockShortage(line.Book.Id, line.Book.Title, line.Quantity, line.Book.Stock))
            .ToList();

        if (shortages.Count > 0)
        {
            throw new InsufficientStockException(shortages);
        }

        foreach (var line in lines)
        {
            line.Book.RemoveStock(line.Quantity);
        }

        return new Order
        {
            PlacedAt = placedAt,
            CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant(),
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Tax = quote.Tax,
            Total = quote.Total,
            Lines = [.. lines.Select(line => new OrderLine
            {
                BookId = line.Book.Id,
                Book = line.Book,
                Quantity = line.Quantity,
                UnitPrice = line.Book.Price,
            })],
        };
    }
}

/// <summary>One book on an order, with the price it sold at.</summary>
public sealed class OrderLine
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int BookId { get; set; }

    public Book? Book { get; set; }

    public int Quantity { get; set; }

    /// <summary>The book's price when the order was placed. Later price changes don't affect it.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>A book that can't fill its order line.</summary>
public sealed record StockShortage(int BookId, string Title, int Requested, int Available);

/// <summary>Thrown when an order asks for more copies than are on the shelf.</summary>
public sealed class InsufficientStockException(IReadOnlyList<StockShortage> shortages)
    : InvalidOperationException(Describe(shortages))
{
    public IReadOnlyList<StockShortage> Shortages { get; } = shortages;

    private static string Describe(IReadOnlyList<StockShortage> shortages) =>
        "Not enough stock: " + string.Join("; ", shortages.Select(s =>
            $"'{s.Title}' (book {s.BookId}) has {s.Available} of {s.Requested} requested"));
}
