using Microsoft.EntityFrameworkCore;
using ToolsApp.Domain;

namespace ToolsApp.Api.Data;

/// <summary>Starter palettes loaded the first time the database is created.</summary>
public static class SeedData
{
    public static void Seed(ToolsAppDbContext db)
    {
        if (db.Palettes.Any())
        {
            return;
        }

        db.Palettes.AddRange(CreatePalettes());
        db.SaveChanges();
    }

    public static async Task SeedAsync(ToolsAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Palettes.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Palettes.AddRange(CreatePalettes());
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<Palette> CreatePalettes() =>
    [
        new()
        {
            Name = "Primary",
            Colors = [NewColor("Red", "#FF0000"), NewColor("Green", "#00FF00"), NewColor("Blue", "#0000FF")],
        },
        new()
        {
            Name = "Ocean",
            Colors = [NewColor("Deep Sea", "#003B5C"), NewColor("Lagoon", "#00A6A6"), NewColor("Foam", "#E0F7F5")],
        },
        new()
        {
            Name = "Sunset",
            Colors = [NewColor("Coral", "#FF6F59"), NewColor("Amber", "#FFB000"), NewColor("Dusk", "#5B2A86")],
        },
    ];

    private static Color NewColor(string name, string hex) => new() { Name = name, Hex = hex };
}
