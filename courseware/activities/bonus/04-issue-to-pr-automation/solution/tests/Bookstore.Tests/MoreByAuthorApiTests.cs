using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;

namespace Bookstore.Tests;

public class MoreByAuthorApiTests(BookstoreApiFactory factory) : IClassFixture<BookstoreApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Lists_the_authors_other_books_that_are_in_stock()
    {
        var books = await GetAsync("/api/books/1/more-by-author");

        Assert.Equal(["Rivers of Glass"], books.Select(b => b.Title));
    }

    [Theory]
    [InlineData("/api/books/3/more-by-author")]
    [InlineData("/api/books/3/more-by-author?includeOutOfStock=false")]
    public async Task Leaves_out_of_stock_books_out_unless_asked(string url)
    {
        var books = await GetAsync(url);

        Assert.Empty(books);
    }

    [Fact]
    public async Task Includes_out_of_stock_books_when_asked()
    {
        var books = await GetAsync("/api/books/3/more-by-author?includeOutOfStock=true");

        var book = Assert.Single(books);
        Assert.Equal(("Northern Lights Field Guide", 0), (book.Title, book.Stock));
    }

    [Fact]
    public async Task Never_includes_the_book_itself()
    {
        var books = await GetAsync("/api/books/2/more-by-author?includeOutOfStock=true");

        Assert.DoesNotContain(books, b => b.Id == 2);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_book()
    {
        var response = await _client.GetAsync("/api/books/9999/more-by-author", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<BookDto>> GetAsync(string url) =>
        await _client.GetFromJsonAsync<List<BookDto>>(url, Json, TestContext.Current.CancellationToken) ?? [];
}
