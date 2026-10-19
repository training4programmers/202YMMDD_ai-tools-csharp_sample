using System.Net;
using System.Net.Http.Json;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

public class ColorSearchApiTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("sea")]
    [InlineData("DEEP")]
    public async Task Search_matches_part_of_a_name_ignoring_case(string name)
    {
        var colors = await SearchAsync(name);

        Assert.Equal(["Deep Sea"], colors.Select(c => c.Name));
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Search_treats_sql_and_wildcard_characters_as_plain_text(string name)
    {
        var colors = await SearchAsync(name);

        Assert.Empty(colors);
    }

    [Fact]
    public async Task Search_without_a_name_is_a_bad_request()
    {
        var response = await _client.GetAsync("/api/colors/search", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<List<ColorDto>> SearchAsync(string name) =>
        await _client.GetFromJsonAsync<List<ColorDto>>(
            $"/api/colors/search?name={Uri.EscapeDataString(name)}", TestContext.Current.CancellationToken) ?? [];
}
