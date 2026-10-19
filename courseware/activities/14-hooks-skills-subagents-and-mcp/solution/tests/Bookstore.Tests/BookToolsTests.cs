using Bookstore.Domain;
using Bookstore.Mcp;
using ModelContextProtocol;

namespace Bookstore.Tests;

/// <summary>
/// Tests the MCP tool methods directly. No MCP client is involved: a tool is just a method, so the
/// interesting behavior (validation, limits, what gets returned) is plain unit-testable C#.
/// </summary>
public class BookToolsTests
{
    private const string Patterns = "978-1-23456-005-8";
    private const string Rivers = "978-1-23456-002-7";
    private const string LighthouseCat = "978-1-23456-007-2";

    private readonly FakeBookCatalog _catalog = new()
    {
        Books =
        [
            new BookMatch(1, "The Quiet Compiler", "Maya Okafor", "978-1-23456-001-0", 18.99m, 12),
            new BookMatch(2, "Rivers of Glass", "Maya Okafor", Rivers, 24.50m, 3),
            new BookMatch(5, "Patterns in the Noise", "Priya Raman", Patterns, 45.00m, 20),
            new BookMatch(7, "The Lighthouse Cat", "Tomás Ortega", LighthouseCat, 12.99m, 30),
        ],
    };

    private BookTools CreateTools() => new(_catalog, new PriceCalculator());

    [Fact]
    public async Task Find_books_trims_the_query_and_caps_the_results()
    {
        var matches = await CreateTools().FindBooksAsync("  compiler ", TestContext.Current.CancellationToken);

        Assert.Equal("compiler", _catalog.LastQuery);
        Assert.Equal(BookTools.MaxResults, _catalog.LastLimit);
        Assert.Equal("The Quiet Compiler", Assert.Single(matches).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Find_books_rejects_an_empty_query(string query) =>
        await Assert.ThrowsAsync<McpException>(
            () => CreateTools().FindBooksAsync(query, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData("978-1-23456-002-7")]
    [InlineData("9781234560027")]
    public async Task Check_stock_finds_a_book_with_or_without_hyphens(string isbn)
    {
        var stock = await CreateTools().CheckStockAsync(isbn, TestContext.Current.CancellationToken);

        Assert.Equal(new StockLevel(Rivers, "Rivers of Glass", 3, InStock: true), stock);
    }

    [Theory]
    [InlineData("978-1-23456-002-0")] // bad check digit
    [InlineData("9781234560096")]     // valid, but not in the catalog
    public async Task Check_stock_explains_bad_or_unknown_isbns(string isbn) =>
        await Assert.ThrowsAsync<McpException>(
            () => CreateTools().CheckStockAsync(isbn, TestContext.Current.CancellationToken));

    [Fact]
    public async Task Quote_order_uses_the_real_pricing_rules()
    {
        OrderLineInput[] lines = [new(Patterns, 10), new(LighthouseCat, 1)];

        var quote = await CreateTools().QuoteOrderAsync(lines, "readmore", TestContext.Current.CancellationToken);

        Assert.Equal((462.99m, 50.00m, 24.78m, 437.77m), (quote.Subtotal, quote.Discount, quote.Tax, quote.Total));
        Assert.True(quote.CanFulfil);
        Assert.Equal(["Patterns in the Noise", "The Lighthouse Cat"], quote.Lines.Select(l => l.Title));
    }

    [Fact]
    public async Task Quote_order_flags_lines_the_shelf_cannot_fill()
    {
        OrderLineInput[] lines = [new(Rivers, 4)];

        var quote = await CreateTools().QuoteOrderAsync(lines, cancellationToken: TestContext.Current.CancellationToken);

        var line = Assert.Single(quote.Lines);
        Assert.False(line.EnoughStock);
        Assert.Equal(3, line.InStock);
        Assert.False(quote.CanFulfil);
    }

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public async Task Quote_order_rejects_invalid_requests(OrderLineInput[] lines, string? coupon) =>
        await Assert.ThrowsAsync<McpException>(
            () => CreateTools().QuoteOrderAsync(lines, coupon, TestContext.Current.CancellationToken));

    public static TheoryData<OrderLineInput[], string?> InvalidOrders() => new()
    {
        { [], null },
        { [new(Patterns, 0)], null },
        { [new("978-1-23456-005-0", 1)], null },              // bad check digit
        { [new("9781234560096", 1)], null },                  // not in the catalog
        { [new(Patterns, 1), new("9781234560058", 2)], null }, // same book twice
        { [new(Patterns, 1)], "FREEBOOKS" },
    };

    private sealed class FakeBookCatalog : IBookCatalog
    {
        public IReadOnlyList<BookMatch> Books { get; init; } = [];

        public string? LastQuery { get; private set; }

        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<BookMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
        {
            LastQuery = text;
            LastLimit = limit;
            IReadOnlyList<BookMatch> matches =
                [.. Books.Where(b => b.Title.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(limit)];
            return Task.FromResult(matches);
        }

        public Task<IReadOnlyList<BookMatch>> FindByIsbnsAsync(
            IReadOnlyCollection<string> normalizedIsbns, CancellationToken cancellationToken)
        {
            IReadOnlyList<BookMatch> matches = [.. Books.Where(b => normalizedIsbns.Contains(Isbn.Normalize(b.Isbn)))];
            return Task.FromResult(matches);
        }
    }
}
