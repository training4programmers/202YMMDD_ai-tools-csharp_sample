using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

public class ColorSearchApiTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("lagoon", "Lagoon")]
    [InlineData("DEEP", "Deep Sea")]
    [InlineData("#5b2a86", "Dusk")]
    public async Task Search_matches_names_and_hex_values(string query, string expected)
    {
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>(
            $"/api/colors/search?q={Uri.EscapeDataString(query)}", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(colors);
        Assert.Contains(colors, c => c.Name == expected);
    }

    [Fact]
    public async Task Search_results_include_the_css_hsl_value()
    {
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>(
            "/api/colors/search?q=red", Json, TestContext.Current.CancellationToken);

        Assert.Equal("hsl(0 100% 50%)", Assert.Single(colors!).CssHsl);
    }
}
