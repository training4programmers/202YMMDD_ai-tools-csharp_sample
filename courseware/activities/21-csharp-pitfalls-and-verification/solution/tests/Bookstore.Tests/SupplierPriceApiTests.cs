using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Tests;

public class SupplierPriceApiTests(BookstoreApiFactory factory) : IClassFixture<BookstoreApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Import_applies_the_sample_price_list()
    {
        const string csv = "isbn,price\n9781234560010,19.49\n978-1-23456-005-8,42.00\n123456002x,25.99\n9781234560072,13.49\n";

        var response = await PostCsvAsync(csv);
        var result = await response.Content.ReadFromJsonAsync<PriceImportResultDto>(Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, result?.Updated);
        Assert.Equal(19.49m, await PriceOfAsync("The Quiet Compiler"));
        Assert.Equal(42.00m, await PriceOfAsync("Patterns in the Noise"));
        Assert.Equal(25.99m, await PriceOfAsync("Rivers of Glass"));
        Assert.Equal(13.49m, await PriceOfAsync("The Lighthouse Cat"));
    }

    [Fact]
    public async Task Import_with_a_bad_row_returns_400_and_changes_nothing()
    {
        var response = await PostCsvAsync("isbn,price\n9781234560034,35.00\n9781234560041,-1\n");
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(
            Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.StartsWith("Line 3:", Assert.Single(problem.Errors["csv"]), StringComparison.Ordinal);
        Assert.Equal(32.00m, await PriceOfAsync("A Short History of Bridges"));
    }

    [Fact]
    public async Task Import_of_an_isbn_that_is_not_in_the_catalog_returns_400()
    {
        // 978-1-23456-009-6 is a well-formed ISBN-13, but no seeded book has it.
        var response = await PostCsvAsync("9781234560096,10.00\n9781234560034,35.00\n");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(32.00m, await PriceOfAsync("A Short History of Bridges"));
    }

    private async Task<HttpResponseMessage> PostCsvAsync(string csv)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await _client.PostAsync("/api/books/prices/import", content, TestContext.Current.CancellationToken);
    }

    private async Task<decimal> PriceOfAsync(string title)
    {
        var books = await _client.GetFromJsonAsync<List<BookDto>>("/api/books", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(books);
        return books.Single(b => b.Title == title).Price;
    }
}
