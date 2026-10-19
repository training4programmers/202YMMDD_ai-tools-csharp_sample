using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ToolsApp.Api.Data;
using ToolsApp.Mcp;

namespace ToolsApp.Tests;

/// <summary>
/// Checks the MCP server's SQL against the schema the API really creates: the database is built with the
/// API's own <see cref="ToolsAppDbContext"/> and seed data, then read through the read-only catalog.
/// </summary>
public sealed class SqliteColorCatalogTests : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"toolsapp-mcp-tests-{Guid.NewGuid():N}.db");

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ToolsAppDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .Options;

        await using var db = new ToolsAppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedData.SeedAsync(db, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_databasePath) + "*"))
        {
            File.Delete(file);
        }

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("sea", "Deep Sea")]
    [InlineData("LAGOON", "Lagoon")]
    [InlineData("#5B2A", "Dusk")]
    public async Task Search_matches_names_and_hex_values_case_insensitively(string text, string expected)
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync(text, 25, TestContext.Current.CancellationToken);

        Assert.Contains(matches, c => c.Name == expected);
    }

    [Fact]
    public async Task Search_matches_palette_names()
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync("sunset", 25, TestContext.Current.CancellationToken);

        Assert.Equal(["Amber", "Coral", "Dusk"], matches.Select(c => c.Name));
        Assert.All(matches, c => Assert.Equal("Sunset", c.Palette));
    }

    [Fact]
    public async Task Search_treats_like_wildcards_as_literal_text()
    {
        var matches = await CreateCatalog(_databasePath).SearchAsync("%", 25, TestContext.Current.CancellationToken);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_explains_a_missing_database()
    {
        var catalog = CreateCatalog(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db"));

        var error = await Assert.ThrowsAsync<McpException>(
            () => catalog.SearchAsync("sea", 25, TestContext.Current.CancellationToken));

        Assert.Contains("dotnet run --project src/ToolsApp.Api", error.Message, StringComparison.Ordinal);
    }

    private static SqliteColorCatalog CreateCatalog(string path) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
}
