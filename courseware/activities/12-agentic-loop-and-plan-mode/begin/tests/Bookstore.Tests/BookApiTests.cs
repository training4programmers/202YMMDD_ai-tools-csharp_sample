using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;

namespace Bookstore.Tests;

public class BookApiTests(BookstoreApiFactory factory) : IClassFixture<BookstoreApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_books_returns_the_seeded_catalog()
    {
        var books = await _client.GetFromJsonAsync<List<BookDto>>(
            "/api/books", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(books);
        Assert.Contains(books, b => b.Title == "The Quiet Compiler" && b.AuthorName == "Maya Okafor");
    }

    [Fact]
    public async Task Get_unknown_book_returns_404()
    {
        var response = await _client.GetAsync("/api/books/9999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_invalid_isbn_returns_validation_problem()
    {
        var request = new
        {
            title = "Bad Checksum",
            isbn = "978-1-23456-009-0",
            price = 10m,
            stock = 1,
            genre = "Fiction",
            publishedOn = "2025-05-01",
            authorId = 1,
        };

        var response = await _client.PostAsJsonAsync("/api/books", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
