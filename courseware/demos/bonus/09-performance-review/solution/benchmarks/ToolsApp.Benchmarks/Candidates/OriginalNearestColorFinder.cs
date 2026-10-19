// The AI-generated finder exactly as it was before the optimization, kept for two jobs: the
// benchmark baseline, and the reference the equivalence tests compare the new finder against
// (tests/ToolsApp.Tests links this file). Don't "fix" it; it's evidence.
using ToolsApp.Domain;

namespace ToolsApp.Benchmarks.Candidates;

/// <summary>
/// Finds the closest named color to any hex color, by straight-line distance in RGB space.
/// </summary>
public class OriginalNearestColorFinder
{
    private readonly IEnumerable<NamedColor> _colors;

    public OriginalNearestColorFinder(IEnumerable<NamedColor> colors)
    {
        _colors = colors;
    }

    /// <summary>The named color closest to <paramref name="hex"/>.</summary>
    public ColorMatch FindNearest(string hex)
    {
        return _colors
            .Select(c => new ColorMatch(c.Name, c.Hex, Distance(hex, c.Hex)))
            .OrderBy(m => m.Distance)
            .First();
    }

    /// <summary>The closest named color for each of <paramref name="hexes"/>, in the same order.</summary>
    public List<ColorMatch> FindNearest(IEnumerable<string> hexes)
    {
        var results = new List<ColorMatch>();

        foreach (var hex in hexes)
        {
            // Take a snapshot of the colors so each lookup works from a stable list.
            var candidates = _colors.ToList();

            var best = candidates
                .Select(c => new ColorMatch(c.Name, c.Hex, Distance(hex, c.Hex)))
                .OrderBy(m => m.Distance)
                .First();

            results.Add(best);
        }

        return results;
    }

    private static double Distance(string first, string second)
    {
        var a = ColorMath.ParseHex(first);
        var b = ColorMath.ParseHex(second);

        return Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
    }
}
