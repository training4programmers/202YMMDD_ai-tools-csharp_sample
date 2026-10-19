using Bookstore.Mcp;
using ModelContextProtocol;

namespace Bookstore.Tests;

/// <summary>
/// Tests the MCP tool methods directly. No MCP client is involved: a tool is just a method, so the
/// interesting behavior (validation, limits, what gets returned) is plain unit-testable C#.
/// </summary>
public class BookToolsTests
{
    private readonly FakeBookCatalog _catalog = new()
    {
        Books =
        [
            new BookMatch(1, "The Quiet Compiler", "Maya Okafor", "978-1-23456-001-0", 18.99m, 12),
            new BookMatch(2, "Rivers of Glass", "Maya Okafor", "978-1-23456-002-7", 24.50m, 3),
        ],
    };

    [Fact]
    public async Task Find_books_trims_the_query_and_caps_the_results()
    {
        var tools = new BookTools(_catalog);

        var matches = await tools.FindBooksAsync("  compiler ", TestContext.Current.CancellationToken);

        Assert.Equal("compiler", _catalog.LastQuery);
        Assert.Equal(BookTools.MaxResults, _catalog.LastLimit);
        Assert.Equal("The Quiet Compiler", Assert.Single(matches).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Find_books_rejects_an_empty_query(string query)
    {
        var tools = new BookTools(_catalog);

        await Assert.ThrowsAsync<McpException>(
            () => tools.FindBooksAsync(query, TestContext.Current.CancellationToken));
    }

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
    }
}
