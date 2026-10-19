using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;

namespace Bookstore.Tests;

public class BookLookupApiTests(BookstoreApiFactory factory) : IClassFixture<BookstoreApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("978-1-23456-001-0")]
    [InlineData("9781234560010")]
    public async Task By_isbn_finds_the_book_with_or_without_hyphens(string isbn)
    {
        var book = await _client.GetFromJsonAsync<BookDto>($"/api/books/by-isbn/{isbn}", Json, TestContext.Current.CancellationToken);

        Assert.Equal("The Quiet Compiler", book?.Title);
    }

    [Fact]
    public async Task By_isbn_returns_404_for_a_valid_isbn_that_is_not_in_the_catalog()
    {
        var response = await _client.GetAsync("/api/books/by-isbn/9781234560096", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task By_isbn_rejects_a_malformed_isbn()
    {
        var response = await _client.GetAsync("/api/books/by-isbn/978-1-23456-001-9", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
