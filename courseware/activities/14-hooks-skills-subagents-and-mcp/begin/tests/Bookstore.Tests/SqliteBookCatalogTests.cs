using Bookstore.Api.Data;
using Bookstore.Mcp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace Bookstore.Tests;

/// <summary>
/// Checks the MCP server's SQL against the schema the API really creates: the database is built with the
/// API's own <see cref="BookstoreDbContext"/> and seed data, then read through the read-only catalog.
/// </summary>
public sealed class SqliteBookCatalogTests : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"bookstore-mcp-tests-{Guid.NewGuid():N}.db");

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<BookstoreDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .Options;

        await using var db = new BookstoreDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedData.SeedAsync(db, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_databasePath) + "*"))
        {
            File.Delete(file);
        }

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("compiler", "The Quiet Compiler")]
    [InlineData("OKAFOR", "Rivers of Glass")]
    [InlineData("978-1-23456-005", "Patterns in the Noise")]
    [InlineData("9781234560065", "Async All the Way Down")]
    public async Task Search_matches_titles_authors_and_isbns(string text, string expected)
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync(text, 25, TestContext.Current.CancellationToken);

        Assert.Contains(matches, b => b.Title == expected);
    }

    [Fact]
    public async Task Search_returns_prices_as_decimals()
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync("Lighthouse", 25, TestContext.Current.CancellationToken);

        var book = Assert.Single(matches);
        Assert.Equal(12.99m, book.Price);
        Assert.Equal(30, book.Stock);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_as_literal_text()
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync("_", 25, TestContext.Current.CancellationToken);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_explains_a_missing_database()
    {
        var catalog = CreateCatalog(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db"));

        var error = await Assert.ThrowsAsync<McpException>(
            () => catalog.SearchAsync("compiler", 25, TestContext.Current.CancellationToken));

        Assert.Contains("dotnet run --project src/Bookstore.Api", error.Message, StringComparison.Ordinal);
    }

    private static SqliteBookCatalog CreateCatalog(string path) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
}
