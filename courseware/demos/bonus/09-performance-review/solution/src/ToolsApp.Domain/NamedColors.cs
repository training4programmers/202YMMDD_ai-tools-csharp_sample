namespace ToolsApp.Domain;

/// <summary>A color with a human-friendly name, as <c>#RRGGBB</c>.</summary>
public sealed record NamedColor(string Name, string Hex);

/// <summary>
/// The built-in list of 1,000 named colors ("Pale Vivid Red" to "Deep Dusty Crimson"), loaded once
/// from the embedded <c>NamedColors.csv</c>.
/// </summary>
public static class NamedColors
{
    private static readonly Lazy<IReadOnlyList<NamedColor>> Colors = new(Load);

    public static IReadOnlyList<NamedColor> All => Colors.Value;

    private static List<NamedColor> Load()
    {
        using var stream = typeof(NamedColors).Assembly.GetManifestResourceStream("ToolsApp.Domain.NamedColors.csv")
            ?? throw new InvalidOperationException("The NamedColors.csv resource is missing from ToolsApp.Domain.");
        using var reader = new StreamReader(stream);

        var colors = new List<NamedColor>();
        reader.ReadLine(); // header: name,hex
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var comma = line.IndexOf(',', StringComparison.Ordinal);
            colors.Add(new NamedColor(line[..comma], line[(comma + 1)..]));
        }

        return colors;
    }
}
