using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ToolsApp.Mcp;

/// <summary>
/// Finds the SQLite file the ToolsApp API creates, and always opens it read-only.
/// </summary>
/// <remarks>
/// An AI client starts this server from a working directory we don't control, so the default path is
/// resolved from the solution folder instead of the current directory. The API creates <c>toolsapp.db</c>
/// in its own project folder (both <c>dotnet run --project</c> and Visual Studio start it there). Set the
/// <c>ConnectionStrings__ToolsApp</c> environment variable to use a different file.
/// </remarks>
public static class ToolsAppDatabase
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("ToolsApp");
        var builder = new SqliteConnectionStringBuilder(configured ?? $"Data Source={DefaultPath()}")
        {
            // Least privilege, enforced by SQLite itself: no tool on this server can write, whatever a model asks for.
            Mode = SqliteOpenMode.ReadOnly,
        };

        return builder.ToString();
    }

    private static string DefaultPath()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "ToolsApp.slnx")))
            {
                return Path.Combine(folder.FullName, "src", "ToolsApp.Api", "toolsapp.db");
            }
        }

        return Path.GetFullPath("toolsapp.db");
    }
}
