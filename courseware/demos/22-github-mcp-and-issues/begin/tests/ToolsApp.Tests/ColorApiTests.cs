using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

public class ColorApiTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
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
    public async Task Get_colors_returns_the_seeded_palettes()
    {
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>(
            "/api/colors", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(colors);
        Assert.Contains(colors, c => c.Name == "Lagoon" && c.PaletteName == "Ocean");
    }

    [Fact]
    public async Task Get_unknown_color_returns_404()
    {
        var response = await _client.GetAsync("/api/colors/9999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_malformed_hex_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/colors", new { name = "Bad", hex = "#GG0000", paletteId = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
