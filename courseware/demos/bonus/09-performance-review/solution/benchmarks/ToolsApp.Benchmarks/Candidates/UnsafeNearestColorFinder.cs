// An AI-suggested "further optimization" of NearestColorFinder: skip array bounds checks with
// Unsafe.Add and force inlining of the distance math. Measured in BENCHMARKS.md and rejected; it stays
// here only so the measurement can be repeated.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ToolsApp.Domain;

namespace ToolsApp.Benchmarks.Candidates;

public sealed class UnsafeNearestColorFinder
{
    private readonly NamedColor[] _colors;
    private readonly Rgb[] _rgb;

    public UnsafeNearestColorFinder(IEnumerable<NamedColor> colors)
    {
        _colors = [.. colors];
        _rgb = Array.ConvertAll(_colors, c => ColorMath.ParseHex(c.Hex));
    }

    public ColorMatch FindNearest(string hex)
    {
        var target = ColorMath.ParseHex(hex);
        ref var first = ref MemoryMarshal.GetArrayDataReference(_rgb);
        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < _rgb.Length; i++)
        {
            var distance = SquaredDistance(Unsafe.Add(ref first, i), target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        var best = _colors[bestIndex];
        return new ColorMatch(best.Name, best.Hex, Math.Sqrt(bestDistance));
    }

    public List<ColorMatch> FindNearest(IEnumerable<string> hexes)
    {
        var results = new List<ColorMatch>();
        foreach (var hex in hexes)
        {
            results.Add(FindNearest(hex));
        }

        return results;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SquaredDistance(Rgb a, Rgb b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return (dr * dr) + (dg * dg) + (db * db);
    }
}
