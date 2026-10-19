using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ToolsApp.Tests;

/// <summary>
/// Boots the real API in memory, pointed at a throwaway SQLite file so every test class starts
/// from the same seeded palettes.
/// </summary>
public sealed class ToolsAppApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"toolsapp-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ToolsApp", $"Data Source={_databasePath};Pooling=False");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_databasePath);
    }
}
