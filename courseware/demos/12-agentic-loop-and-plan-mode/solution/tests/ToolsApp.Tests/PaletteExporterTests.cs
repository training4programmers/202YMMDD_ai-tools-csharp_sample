using System.Text.Json;
using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class PaletteExporterTests
{
    private static Palette Ocean() => new()
    {
        Name = "Ocean",
        Colors =
        [
            new() { Id = 6, Name = "Foam", Hex = "#E0F7F5" },
            new() { Id = 4, Name = "Deep Sea", Hex = "#003B5C" },
            new() { Id = 5, Name = "Lagoon", Hex = "#00A6A6" },
        ],
    };

    [Theory]
    [InlineData("Deep Sea", "deep-sea")]
    [InlineData("  Sunset & Dusk!! ", "sunset-dusk")]
    [InlineData("Teal 2", "teal-2")]
    [InlineData("Crème Brûlée", "cr-me-br-l-e")]
    public void Slugify_keeps_ascii_letters_and_digits_joined_by_single_hyphens(string name, string expected) =>
        Assert.Equal(expected, PaletteExporter.Slugify(name));

    [Theory]
    [InlineData("css", PaletteExportFormat.Css)]
    [InlineData("JSON", PaletteExportFormat.Json)]
    [InlineData("Gpl", PaletteExportFormat.Gpl)]
    public void TryParseFormat_ignores_case(string value, PaletteExportFormat expected)
    {
        Assert.True(PaletteExporter.TryParseFormat(value, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pdf")]
    [InlineData("1")] // Enum.TryParse would accept this
    public void TryParseFormat_rejects_unknown_values(string? value) =>
        Assert.False(PaletteExporter.TryParseFormat(value, out _));

    [Fact]
    public void ToCss_writes_one_custom_property_per_color_in_id_order()
    {
        const string expected = """
            /* Ocean palette exported from ToolsApp */
            :root {
              --ocean-deep-sea: #003B5C;
              --ocean-lagoon: #00A6A6;
              --ocean-foam: #E0F7F5;
            }

            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), PaletteExporter.ToCss(Ocean()));
    }

    [Fact]
    public void ToGpl_writes_the_gimp_format_with_lf_line_endings()
    {
        var expected =
            "GIMP Palette\nName: Ocean\nColumns: 3\n#\n" +
            "  0  59  92\tDeep Sea\n" +
            "  0 166 166\tLagoon\n" +
            "224 247 245\tFoam\n";

        var gpl = PaletteExporter.ToGpl(Ocean());

        Assert.Equal(expected, gpl);
        Assert.DoesNotContain('\r', gpl);
    }

    [Fact]
    public void ToJson_includes_name_hex_and_rgb_for_each_color()
    {
        using var json = JsonDocument.Parse(PaletteExporter.ToJson(Ocean()));
        var root = json.RootElement;

        Assert.Equal("Ocean", root.GetProperty("name").GetString());
        var first = root.GetProperty("colors")[0];
        Assert.Equal("Deep Sea", first.GetProperty("name").GetString());
        Assert.Equal("#003B5C", first.GetProperty("hex").GetString());
        Assert.Equal(59, first.GetProperty("rgb").GetProperty("g").GetInt32());
    }

    [Theory]
    [InlineData(PaletteExportFormat.Css, "ocean.css", "text/css; charset=utf-8")]
    [InlineData(PaletteExportFormat.Json, "ocean.json", "application/json; charset=utf-8")]
    [InlineData(PaletteExportFormat.Gpl, "ocean.gpl", "application/x-gimp-palette")]
    public void Export_names_the_file_and_media_type(PaletteExportFormat format, string fileName, string contentType)
    {
        var export = PaletteExporter.Export(Ocean(), format);

        Assert.Equal(fileName, export.FileName);
        Assert.Equal(contentType, export.ContentType);
    }
}
