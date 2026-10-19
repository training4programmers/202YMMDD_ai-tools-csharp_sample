using System.Globalization;
using ToolsApp.Domain;

namespace ToolsApp.Tests;

public class PaletteCssTests
{
    [Fact]
    public void Render_writes_one_custom_property_per_color_in_id_order()
    {
        var palette = new Palette
        {
            Name = "Deep  Ocean",
            Colors =
            [
                new Color { Id = 7, Name = "Foam", Hex = "#E0F7F5" },
                new Color { Id = 3, Name = "Deep Sea", Hex = "#003B5C" },
            ],
        };

        var css = PaletteCss.Render(palette);

        Assert.Equal(":root {\n  --deep-ocean-deep-sea: #003B5C;\n  --deep-ocean-foam: #E0F7F5;\n}\n", css);
    }

    [Fact]
    public void Slug_is_the_same_in_every_culture()
    {
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        Assert.Equal("ındigo", "Indigo".ToLower(turkish)); // what culture-sensitive casing does

        using var scope = new CultureScope("tr-TR");

        Assert.Equal("indigo", PaletteCss.Slug("Indigo"));
    }
}
