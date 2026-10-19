using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

public class PaletteTransferApiTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Export_returns_css_custom_properties_with_an_etag()
    {
        var id = await PaletteIdAsync("Primary");

        var response = await _client.GetAsync($"/api/palettes/{id}/export.css", TestContext.Current.CancellationToken);
        var css = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("  --primary-red: #FF0000;\n", css, StringComparison.Ordinal);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Export_returns_304_when_the_etag_still_matches()
    {
        var id = await PaletteIdAsync("Primary");
        var first = await _client.GetAsync($"/api/palettes/{id}/export.css", TestContext.Current.CancellationToken);
        Assert.NotNull(first.Headers.ETag);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/palettes/{id}/export.css");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag);
        var second = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Export_of_an_unknown_palette_returns_404()
    {
        var response = await _client.GetAsync("/api/palettes/9999/export.css", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Import_adds_hex_and_hsl_colors_to_the_palette()
    {
        var id = await PaletteIdAsync("Sunset");

        var response = await PostCsvAsync(id, "name,value\nKelp,#2e8b57\nDune,hsl(38, 76%, 81%)\n");
        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>(Json, TestContext.Current.CancellationToken);
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>(
            $"/api/palettes/{id}/colors", Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result?.Imported);
        Assert.NotNull(colors);
        Assert.Contains(colors, c => c.Name == "Kelp" && c.Hex == "#2E8B57");
        Assert.Contains(colors, c => c.Name == "Dune" && c.Hex == "#F3D8AA");
    }

    [Fact]
    public async Task Import_with_a_bad_line_returns_400_and_saves_nothing()
    {
        var id = await PaletteIdAsync("Ocean");

        var response = await PostCsvAsync(id, "Kelp,#2E8B57\nBroken,#GG0000\n");
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>(
            $"/api/palettes/{id}/colors", Json, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(colors);
        Assert.DoesNotContain(colors, c => c.Name == "Kelp");
    }

    [Fact]
    public async Task Import_into_an_unknown_palette_returns_404()
    {
        var response = await PostCsvAsync(9999, "Kelp,#2E8B57\n");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> PaletteIdAsync(string name)
    {
        var palettes = await _client.GetFromJsonAsync<List<PaletteDto>>(
            "/api/palettes", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(palettes);
        return palettes.Single(p => p.Name == name).Id;
    }

    private async Task<HttpResponseMessage> PostCsvAsync(int paletteId, string csv)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await _client.PostAsync($"/api/palettes/{paletteId}/import", content, TestContext.Current.CancellationToken);
    }
}
