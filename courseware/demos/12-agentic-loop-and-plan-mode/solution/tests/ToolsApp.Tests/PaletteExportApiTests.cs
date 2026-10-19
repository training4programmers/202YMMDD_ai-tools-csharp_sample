using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

public class PaletteExportApiTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("css", "text/css", "ocean.css")]
    [InlineData("json", "application/json", "ocean.json")]
    [InlineData("GPL", "application/x-gimp-palette", "ocean.gpl")]
    public async Task Export_returns_a_download_with_the_right_media_type(string format, string mediaType, string fileName)
    {
        var id = await OceanIdAsync();

        var response = await _client.GetAsync($"/api/palettes/{id}/export?format={format}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(fileName, response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Gpl_export_matches_the_gimp_format()
    {
        var id = await OceanIdAsync();

        var gpl = await _client.GetStringAsync($"/api/palettes/{id}/export?format=gpl", TestContext.Current.CancellationToken);

        Assert.StartsWith("GIMP Palette\nName: Ocean\nColumns: 3\n#\n  0  59  92\tDeep Sea\n", gpl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_export_lists_the_palette_colors()
    {
        var id = await OceanIdAsync();

        var json = await _client.GetStringAsync($"/api/palettes/{id}/export?format=json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.GetProperty("colors").EnumerateArray().Select(c => c.GetProperty("name").GetString());
        Assert.Equal(["Deep Sea", "Lagoon", "Foam"], names);
    }

    [Fact]
    public async Task Export_of_unknown_palette_returns_404()
    {
        var response = await _client.GetAsync("/api/palettes/9999/export?format=css", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?format=pdf")]
    [InlineData("?format=1")]
    public async Task Export_with_missing_or_unknown_format_returns_a_validation_problem(string query)
    {
        var id = await OceanIdAsync();

        var response = await _client.GetAsync($"/api/palettes/{id}/export{query}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"format\"", body, StringComparison.Ordinal);
    }

    private async Task<int> OceanIdAsync()
    {
        var palettes = await _client.GetFromJsonAsync<List<PaletteDto>>("/api/palettes", TestContext.Current.CancellationToken);
        return Assert.Single(palettes!, p => p.Name == "Ocean").Id;
    }
}
