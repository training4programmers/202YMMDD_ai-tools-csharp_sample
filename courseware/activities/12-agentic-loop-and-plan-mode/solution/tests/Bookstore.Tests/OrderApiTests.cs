using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.Tests;

/// <summary>
/// Orders change stock, so each test gets its own factory (and its own database) instead of sharing one.
/// </summary>
public sealed class OrderApiTests : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BookstoreApiFactory _factory = new();
    private readonly HttpClient _client;

    public OrderApiTests() => _client = _factory.CreateClient();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Post_places_the_order_and_takes_the_stock()
    {
        var request = new { lines = new[] { new { bookId = 5, quantity = 10 }, new { bookId = 7, quantity = 1 } }, couponCode = "READMORE" };

        var response = await _client.PostAsJsonAsync("/api/orders", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(order);
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal((462.99m, 50.00m, 24.78m, 437.77m), (order.Subtotal, order.Discount, order.Tax, order.Total));
        Assert.Equal(["Patterns in the Noise", "The Lighthouse Cat"], order.Lines.Select(l => l.Title));
        Assert.Equal(10, await StockAsync(5));
    }

    [Fact]
    public async Task Get_returns_a_placed_order()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/orders", new { lines = new[] { new { bookId = 1, quantity = 2 } } }, TestContext.Current.CancellationToken);
        var placed = await created.Content.ReadFromJsonAsync<OrderDto>(Json, TestContext.Current.CancellationToken);

        var order = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{placed!.Id}", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(order);
        Assert.Equal(placed.Total, order.Total);
        Assert.Equal(18.99m, Assert.Single(order.Lines).UnitPrice);
    }

    [Fact]
    public async Task Get_unknown_order_returns_404()
    {
        var response = await _client.GetAsync("/api/orders/9999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_too_few_copies_returns_409_and_changes_nothing()
    {
        // Book 1 has 12 copies; book 2 ("Rivers of Glass") has only 3.
        var request = new { lines = new[] { new { bookId = 1, quantity = 1 }, new { bookId = 2, quantity = 4 } } };

        var response = await _client.PostAsJsonAsync("/api/orders", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains("Rivers of Glass", problem?.Detail, StringComparison.Ordinal);
        Assert.Equal(12, await StockAsync(1));
        Assert.Equal(3, await StockAsync(2));
    }

    [Theory]
    [InlineData("""{ "lines": [] }""")]
    [InlineData("""{ "lines": [ { "bookId": 1, "quantity": 0 } ] }""")]
    [InlineData("""{ "lines": [ { "bookId": 9999, "quantity": 1 } ] }""")]
    [InlineData("""{ "lines": [ { "bookId": 1, "quantity": 1 }, { "bookId": 1, "quantity": 2 } ] }""")]
    [InlineData("""{ "lines": [ { "bookId": 1, "quantity": 1 } ], "couponCode": "FREEBOOKS" }""")]
    public async Task Post_with_invalid_request_returns_validation_problem(string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(12, await StockAsync(1));
    }

    [Fact]
    public async Task Stock_is_a_concurrency_token_so_two_orders_cannot_both_take_the_last_copies()
    {
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<BookstoreDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<BookstoreDbContext>();

        // Both read "3 copies of Rivers of Glass", then both try to sell all three.
        var bookInFirst = await first.Books.SingleAsync(b => b.Id == 2, TestContext.Current.CancellationToken);
        var bookInSecond = await second.Books.SingleAsync(b => b.Id == 2, TestContext.Current.CancellationToken);
        bookInFirst.RemoveStock(3);
        bookInSecond.RemoveStock(3);

        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<int> StockAsync(int bookId)
    {
        var book = await _client.GetFromJsonAsync<BookDto>($"/api/books/{bookId}", Json, TestContext.Current.CancellationToken);
        return book!.Stock;
    }
}
