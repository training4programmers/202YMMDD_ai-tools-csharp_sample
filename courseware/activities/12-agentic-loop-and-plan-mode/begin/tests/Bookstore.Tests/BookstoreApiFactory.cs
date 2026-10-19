using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookstore.Tests;

/// <summary>
/// Boots the real API in memory, pointed at a throwaway SQLite file so every test class starts
/// from the same seeded catalog.
/// </summary>
public sealed class BookstoreApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"bookstore-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Bookstore", $"Data Source={_databasePath};Pooling=False");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_databasePath);
    }
}
