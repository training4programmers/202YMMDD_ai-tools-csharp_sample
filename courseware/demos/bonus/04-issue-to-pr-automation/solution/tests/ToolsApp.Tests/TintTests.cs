using System.Net;
using System.Net.Http.Json;
using ToolsApp.Api.Contracts;
using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class TintTests(ToolsAppApiFactory factory) : IClassFixture<ToolsAppApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("#003B5C", 3, new[] { "#406C85", "#809DAE", "#BFCED6" })]
    [InlineData("#FF0000", 1, new[] { "#FF8080" })] // 127.5 rounds away from zero, to 128
    [InlineData("#5B2A86", 5, new[] { "#764E9A", "#9271AE", "#AD95C3", "#C8B8D7", "#E4DCEB" })]
    public void Tints_move_evenly_toward_white(string hex, int count, string[] expected)
    {
        var tints = ColorMath.Tints(ColorMath.ParseHex(hex), count);

        Assert.Equal(expected, tints.Select(ColorMath.ToHex));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Tints_reject_a_count_below_one(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorMath.Tints(new Rgb(0, 0, 0), count));

    [Fact]
    public async Task Endpoint_returns_the_requested_tints()
    {
        var id = await IdOfAsync("Deep Sea");

        var result = await _client.GetFromJsonAsync<TintsDto>($"/api/colors/{id}/tints?count=3", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal((id, "#003B5C"), (result.ColorId, result.BaseHex));
        Assert.Equal(["#406C85", "#809DAE", "#BFCED6"], result.Tints);
    }

    [Fact]
    public async Task Endpoint_defaults_to_five_tints()
    {
        var id = await IdOfAsync("Deep Sea");

        var result = await _client.GetFromJsonAsync<TintsDto>($"/api/colors/{id}/tints", TestContext.Current.CancellationToken);

        Assert.Equal(5, result!.Tints.Count);
        Assert.Equal("#809DAE", result.Tints[2]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Endpoint_rejects_an_out_of_range_count(int count)
    {
        var id = await IdOfAsync("Deep Sea");

        var response = await _client.GetAsync($"/api/colors/{id}/tints?count={count}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_returns_404_for_an_unknown_color()
    {
        var response = await _client.GetAsync("/api/colors/9999/tints", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> IdOfAsync(string name)
    {
        var colors = await _client.GetFromJsonAsync<List<ColorDto>>("/api/colors", TestContext.Current.CancellationToken);
        return Assert.Single(colors!, c => c.Name == name).Id;
    }
}
