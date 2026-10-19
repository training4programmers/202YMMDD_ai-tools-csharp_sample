namespace ToolsApp.Domain;

/// <summary>A named group of colors that are designed to be used together.</summary>
public sealed class Palette
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public List<Color> Colors { get; set; } = [];
}
