namespace ToolsApp.Domain;

/// <summary>A named color, and how far it is from the color you asked about.</summary>
public sealed record ColorMatch(string Name, string Hex, double Distance);

/// <summary>
/// Finds the closest named color to any hex color, by straight-line distance in RGB space.
/// </summary>
/// <remarks>
/// The colors are copied and parsed once, when the finder is created, so a malformed hex in the list
/// fails here rather than on the first lookup, and later changes to the source collection aren't seen.
/// Create the finder once and reuse it.
/// </remarks>
public sealed class NearestColorFinder
{
    private readonly NamedColor[] _colors;
    private readonly Rgb[] _rgb;

    public NearestColorFinder(IEnumerable<NamedColor> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        _colors = [.. colors];
        _rgb = Array.ConvertAll(_colors, c => ColorMath.ParseHex(c.Hex));
    }

    /// <summary>
    /// The named color closest to <paramref name="hex"/>. When two are equally close, the one that comes
    /// first in the list wins.
    /// </summary>
    /// <exception cref="InvalidOperationException">The finder has no colors.</exception>
    /// <exception cref="FormatException"><paramref name="hex"/> isn't a hex color.</exception>
    public ColorMatch FindNearest(string hex)
    {
        if (_rgb.Length == 0)
        {
            throw new InvalidOperationException("There are no named colors to search.");
        }

        var target = ColorMath.ParseHex(hex);
        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < _rgb.Length; i++)
        {
            // Compare squared distances in integers; the square root only matters for the result.
            var dr = _rgb[i].R - target.R;
            var dg = _rgb[i].G - target.G;
            var db = _rgb[i].B - target.B;
            var distance = (dr * dr) + (dg * dg) + (db * db);

            // Strictly less than: on a tie the earlier color stays, as the stable sort it replaces did.
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        var best = _colors[bestIndex];
        return new ColorMatch(best.Name, best.Hex, Math.Sqrt(bestDistance));
    }

    /// <summary>The closest named color for each of <paramref name="hexes"/>, in the same order.</summary>
    public List<ColorMatch> FindNearest(IEnumerable<string> hexes)
    {
        ArgumentNullException.ThrowIfNull(hexes);

        var results = hexes.TryGetNonEnumeratedCount(out var count) ? new List<ColorMatch>(count) : [];
        foreach (var hex in hexes)
        {
            results.Add(FindNearest(hex));
        }

        return results;
    }
}
