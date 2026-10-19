using System.ComponentModel;
using Bookstore.Domain;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bookstore.Mcp;

/// <summary>How many copies of one book are on the shelf.</summary>
public sealed record StockLevel(string Isbn, string Title, int Stock, bool InStock);

/// <summary>One line of an order to price.</summary>
public sealed record OrderLineInput(
    [property: Description("ISBN-13 of the book, with or without hyphens.")] string Isbn,
    [property: Description("Number of copies, 1 to 1000.")] int Quantity);

/// <summary>A priced order line, with whether the shelf can fill it.</summary>
public sealed record QuotedLine(string Isbn, string Title, int Quantity, decimal UnitPrice, int InStock, bool EnoughStock);

/// <summary>A priced order. Nothing was reserved or saved.</summary>
public sealed record OrderQuote(
    IReadOnlyList<QuotedLine> Lines, decimal Subtotal, decimal Discount, decimal Tax, decimal Total, bool CanFulfil);

/// <summary>
/// The tools this server offers. The model reads each <see cref="DescriptionAttribute"/> to decide when to call
/// a tool and what to pass, so write them for the model, not for a human reader.
/// </summary>
[McpServerToolType]
public sealed class BookTools(IBookCatalog catalog, PriceCalculator calculator)
{
    public const int MaxResults = 25;
    public const int MaxOrderLines = 20;
    public const int MaxQuantity = 1000;

    [McpServerTool(Name = "find_books", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds books in the Bookstore catalog whose title, author name, or ISBN contains the query text " +
                 "(case-insensitive). Returns at most 25 matches with id, title, author, ISBN, price, and stock.")]
    public async Task<IReadOnlyList<BookMatch>> FindBooksAsync(
        [Description("Text to look for, for example 'compiler', 'Okafor', or '978-1-23456'.")] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("query must not be empty.");
        }

        return await catalog.SearchAsync(query.Trim(), MaxResults, cancellationToken);
    }

    [McpServerTool(Name = "check_stock", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Looks up one book by ISBN-13 (hyphens optional) and returns its title and how many copies " +
                 "are on the shelf.")]
    public async Task<StockLevel> CheckStockAsync(
        [Description("ISBN-13 of the book, for example '978-1-23456-001-0' or '9781234560010'.")] string isbn,
        CancellationToken cancellationToken)
    {
        if (!Isbn.IsValid(isbn))
        {
            throw new McpException($"'{isbn}' is not a valid ISBN-13.");
        }

        var matches = await catalog.FindByIsbnsAsync([Isbn.Normalize(isbn)], cancellationToken);
        var book = matches.SingleOrDefault() ?? throw new McpException($"No book with ISBN {isbn} is in the catalog.");

        return new StockLevel(book.Isbn, book.Title, book.Stock, book.Stock > 0);
    }

    [McpServerTool(Name = "quote_order", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Prices an order with the shop's real rules (10% off lines of 10 or more copies, coupons " +
                 "STUDENT10 and READMORE, 6% sales tax) and reports whether each line can be filled from stock. " +
                 "It only calculates: it does not place an order, reserve copies, or change anything.")]
    public async Task<OrderQuote> QuoteOrderAsync(
        [Description("The order lines: 1 to 20, each book at most once.")] IReadOnlyList<OrderLineInput> lines,
        [Description("Optional coupon code, for example STUDENT10 or READMORE.")] string? coupon = null,
        CancellationToken cancellationToken = default)
    {
        var requested = Validate(lines);
        var books = await catalog.FindByIsbnsAsync([.. requested.Select(r => r.Isbn)], cancellationToken);

        var found = books.ToDictionary(b => Isbn.Normalize(b.Isbn));
        var unknown = requested.Where(r => !found.ContainsKey(r.Isbn)).Select(r => r.Isbn).ToList();
        if (unknown.Count > 0)
        {
            throw new McpException($"These ISBNs aren't in the catalog: {string.Join(", ", unknown)}.");
        }

        // Price with the real PriceCalculator, so a quote here always agrees with POST /api/quotes.
        var quoteLines = requested
            .Select(r => new QuoteLine(ToBook(found[r.Isbn]), r.Quantity))
            .ToList();

        Quote quote;
        try
        {
            quote = calculator.Calculate(quoteLines, coupon);
        }
        catch (ArgumentException ex) when (ex.ParamName == "couponCode")
        {
            throw new McpException($"Unknown coupon '{coupon}'. Valid coupons are STUDENT10 and READMORE.");
        }

        var quoted = quoteLines
            .Select(l => new QuotedLine(l.Book.Isbn, l.Book.Title, l.Quantity, l.Book.Price, l.Book.Stock, l.Quantity <= l.Book.Stock))
            .ToList();

        return new OrderQuote(quoted, quote.Subtotal, quote.Discount, quote.Tax, quote.Total, quoted.All(l => l.EnoughStock));
    }

    // Returns each line with a normalized ISBN, in request order, or explains what is wrong.
    private static List<(string Isbn, int Quantity)> Validate(IReadOnlyList<OrderLineInput>? lines)
    {
        if (lines is null || lines.Count is 0 or > MaxOrderLines)
        {
            throw new McpException($"lines must contain 1 to {MaxOrderLines} items.");
        }

        var requested = new List<(string Isbn, int Quantity)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (!Isbn.IsValid(line.Isbn))
            {
                throw new McpException($"'{line.Isbn}' is not a valid ISBN-13.");
            }

            if (line.Quantity is < 1 or > MaxQuantity)
            {
                throw new McpException($"Quantity for {line.Isbn} must be 1 to {MaxQuantity}, but was {line.Quantity}.");
            }

            var isbn = Isbn.Normalize(line.Isbn);
            if (!seen.Add(isbn))
            {
                throw new McpException($"ISBN {line.Isbn} appears on more than one line; combine them into one.");
            }

            requested.Add((isbn, line.Quantity));
        }

        return requested;
    }

    private static Book ToBook(BookMatch match) =>
        new() { Id = match.Id, Title = match.Title, Isbn = match.Isbn, Price = match.Price, Stock = match.Stock };
}
