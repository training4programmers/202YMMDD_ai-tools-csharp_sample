using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class PaletteCsvTests
{
    [Fact]
    public void Parse_reads_hex_and_hsl_values_and_skips_the_header()
    {
        var result = PaletteCsv.Parse("name,value\r\nKelp,#2e8b57\r\n\r\nSand,f4d8a8\r\nDune,hsl(38, 76%, 81%)\r\nInk,#123");

        Assert.True(result.IsValid);
        Assert.Equal(
            [
                new ImportedColor("Kelp", "#2E8B57"),
                new ImportedColor("Sand", "#F4D8A8"),
                new ImportedColor("Dune", "#F3D8AA"),
                new ImportedColor("Ink", "#112233"),
            ],
            result.Colors);
    }

    [Fact]
    public void Parse_only_treats_the_first_line_as_a_header()
    {
        var result = PaletteCsv.Parse("Kelp,#2E8B57\nname,#000000");

        Assert.Equal(["Kelp", "name"], result.Colors.Select(c => c.Name));
    }

    [Fact]
    public void Parse_reports_every_bad_line_by_number()
    {
        var result = PaletteCsv.Parse("Kelp,#2E8B57\nno comma here\n,#FFFFFF\nMoss,green");

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Errors,
            error => Assert.StartsWith("Line 2:", error, StringComparison.Ordinal),
            error => Assert.StartsWith("Line 3:", error, StringComparison.Ordinal),
            error => Assert.StartsWith("Line 4: 'green'", error, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_rejects_names_longer_than_the_column()
    {
        var result = PaletteCsv.Parse($"{new string('x', PaletteCsv.MaxNameLength + 1)},#FFFFFF");

        Assert.Single(result.Errors);
    }
}
