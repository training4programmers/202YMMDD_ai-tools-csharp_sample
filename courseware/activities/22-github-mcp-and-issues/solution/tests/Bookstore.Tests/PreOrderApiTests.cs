using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.Tests;

/// <summary>
/// One test per acceptance criterion in docs/issues/pre-orders.md. "Today" is fixed at 2026-10-16
/// through a replacement <see cref="TimeProvider"/>, and each test gets its own database.
/// </summary>
public class PreOrderApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_pre_order_with_stock_is_rejected()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/books", NewBook(1, "2026-12-01", stock: 5), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStockErrorAsync(response);
    }

    [Fact]
    public async Task Creating_a_pre_order_without_stock_succeeds_and_is_flagged()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/books", NewBook(2, "2026-12-01", stock: 0), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BookDto>(Json, Ct);
        Assert.True(created?.IsPreOrder);
    }

    [Fact]
    public async Task Moving_a_stocked_book_into_the_future_is_rejected_and_changes_nothing()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);
        var books = await client.GetFromJsonAsync<List<BookDto>>("/api/books", Json, Ct) ?? [];
        var compiler = books.Single(b => b.Title == "The Quiet Compiler");

        var update = new
        {
            title = compiler.Title,
            isbn = compiler.Isbn,
            price = compiler.Price,
            stock = compiler.Stock,
            genre = compiler.Genre.ToString(),
            publishedOn = "2027-01-15",
            authorId = compiler.AuthorId,
        };
        var response = await client.PutAsJsonAsync($"/api/books/{compiler.Id}", update, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStockErrorAsync(response);
        var unchanged = await client.GetFromJsonAsync<BookDto>($"/api/books/{compiler.Id}", Json, Ct);
        Assert.Equal(new DateOnly(2019, 4, 2), unchanged?.PublishedOn);
    }

    [Fact]
    public async Task A_pre_order_can_be_quoted_at_the_normal_price()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);
        var created = await client.PostAsJsonAsync("/api/books", NewBook(3, "2026-12-01", stock: 0, price: 20.00m), Ct);
        var book = await created.Content.ReadFromJsonAsync<BookDto>(Json, Ct);
        Assert.NotNull(book);

        var response = await client.PostAsJsonAsync(
            "/api/quotes", new { lines = new[] { new { bookId = book.Id, quantity = 2 } } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<Quote>(Json, Ct);
        Assert.Equal(new Quote(Subtotal: 40.00m, Discount: 0m, Tax: 2.40m, Total: 42.40m), quote);
    }

    [Fact]
    public async Task A_book_published_today_is_not_a_pre_order_and_can_be_stocked()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/books", NewBook(4, "2026-10-16", stock: 5), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BookDto>(Json, Ct);
        Assert.False(created?.IsPreOrder);
    }

    [Fact]
    public async Task Book_lists_report_pre_orders()
    {
        await using var factory = new BookstoreApiFactory();
        var client = CreateClient(factory);
        await client.PostAsJsonAsync("/api/books", NewBook(5, "2027-03-01", stock: 0), Ct);

        var books = await client.GetFromJsonAsync<List<BookDto>>("/api/books", Json, Ct) ?? [];
        var authorBooks = await client.GetFromJsonAsync<List<BookDto>>("/api/authors/1/books", Json, Ct) ?? [];

        Assert.Equal(["Upcoming Title 5"], books.Where(b => b.IsPreOrder).Select(b => b.Title));
        Assert.Contains(authorBooks, b => b.Title == "Upcoming Title 5" && b.IsPreOrder);
        Assert.Contains(authorBooks, b => b.Title == "The Quiet Compiler" && !b.IsPreOrder);
    }

    private static HttpClient CreateClient(BookstoreApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now)))).CreateClient();

    private static object NewBook(int serial, string publishedOn, int stock, decimal price = 25.00m) => new
    {
        title = $"Upcoming Title {serial}",
        isbn = IsbnFor(serial),
        price,
        stock,
        genre = "Fiction",
        publishedOn,
        authorId = 1,
    };

    // A valid, unique ISBN-13 that can't collide with the seeded catalog (978-1-23456-...).
    private static string IsbnFor(int serial)
    {
        var first12 = $"978123459{serial:D3}";
        var sum = first12.Select((digit, i) => (digit - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return first12 + ((10 - (sum % 10)) % 10);
    }

    private static async Task AssertStockErrorAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct);
        Assert.NotNull(problem);
        Assert.Contains("Stock", problem.Errors.Keys);
    }
}
