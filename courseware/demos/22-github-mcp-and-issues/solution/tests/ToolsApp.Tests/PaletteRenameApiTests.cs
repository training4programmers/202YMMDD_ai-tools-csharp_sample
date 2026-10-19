using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ToolsApp.Api.Contracts;

namespace ToolsApp.Tests;

/// <summary>
/// One test per acceptance criterion in docs/issues/palette-rename.md. Each test gets its own
/// factory (and database) because renames change the seeded palettes.
/// </summary>
public class PaletteRenameApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rename_returns_the_palette_with_its_new_name_and_color_count()
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();
        var ocean = await FindPaletteAsync(client, "Ocean");

        var response = await client.PatchAsJsonAsync($"/api/palettes/{ocean.Id}", new { name = "Ocean Blues" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renamed = await response.Content.ReadFromJsonAsync<PaletteDto>(Ct);
        Assert.Equal(new PaletteDto(ocean.Id, "Ocean Blues", 3), renamed);

        var names = (await GetPalettesAsync(client)).Select(p => p.Name).ToList();
        Assert.Contains("Ocean Blues", names);
        Assert.DoesNotContain("Ocean", names);
    }

    [Fact]
    public async Task Rename_trims_surrounding_whitespace()
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();
        var ocean = await FindPaletteAsync(client, "Ocean");

        var response = await client.PatchAsJsonAsync($"/api/palettes/{ocean.Id}", new { name = "  Deep Ocean  " }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Deep Ocean", (await FindPaletteAsync(client, "Deep Ocean")).Name);
    }

    [Theory]
    [InlineData("Sunset")]
    [InlineData("sunset")]
    [InlineData(" SUNSET ")]
    public async Task Rename_to_a_name_another_palette_has_returns_409_and_changes_nothing(string newName)
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();
        var ocean = await FindPaletteAsync(client, "Ocean");

        var response = await client.PatchAsJsonAsync($"/api/palettes/{ocean.Id}", new { name = newName }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Palette name already in use", problem?.Title);
        Assert.Equal("Ocean", (await FindPaletteAsync(client, "Ocean")).Name);
    }

    [Fact]
    public async Task Rename_may_change_the_casing_of_the_palettes_own_name()
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();
        var ocean = await FindPaletteAsync(client, "Ocean");

        var response = await client.PatchAsJsonAsync($"/api/palettes/{ocean.Id}", new { name = "OCEAN" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OCEAN", (await response.Content.ReadFromJsonAsync<PaletteDto>(Ct))?.Name);
    }

    [Fact]
    public async Task Rename_of_an_unknown_palette_returns_404()
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync("/api/palettes/9999", new { name = "Anything" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This palette name is sixty-one characters, one over the limit")]
    public async Task Rename_with_an_invalid_name_returns_a_validation_problem(string newName)
    {
        await using var factory = new ToolsAppApiFactory();
        var client = factory.CreateClient();
        var ocean = await FindPaletteAsync(client, "Ocean");

        var response = await client.PatchAsJsonAsync($"/api/palettes/{ocean.Id}", new { name = newName }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct);
        Assert.NotNull(problem);
        Assert.Contains("Name", problem.Errors.Keys);
    }

    private static async Task<List<PaletteDto>> GetPalettesAsync(HttpClient client) =>
        await client.GetFromJsonAsync<List<PaletteDto>>("/api/palettes", Ct) ?? [];

    private static async Task<PaletteDto> FindPaletteAsync(HttpClient client, string name) =>
        (await GetPalettesAsync(client)).Single(p => p.Name == name);
}
