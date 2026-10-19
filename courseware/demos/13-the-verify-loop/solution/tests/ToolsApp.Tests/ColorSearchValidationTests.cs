using System.Net;

namespace ToolsApp.Tests;

public class ColorSearchValidationTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("")]
    [InlineData("?q=")]
    [InlineData("?q=%20%20")]
    public async Task Search_without_a_query_returns_a_validation_problem(string query)
    {
        var response = await _client.GetAsync($"/api/colors/search{query}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
