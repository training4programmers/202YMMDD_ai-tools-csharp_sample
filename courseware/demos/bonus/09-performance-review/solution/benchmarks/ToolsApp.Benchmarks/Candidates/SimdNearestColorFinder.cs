// An AI-proposed "further optimization": keep the palette as three parallel int arrays and compare
// Vector<int>.Count candidates per step with SIMD. Measured in BENCHMARKS.md: clearly faster, but not
// promoted to NearestColorFinder (see there for why). Adapted from the recorded run after review: the
// arrays are built in the constructor (the agent's version built them lazily into five separate
// fields, which isn't safe if two threads make the first call at once), and without `!`.
using System.Numerics;
using ToolsApp.Domain;

namespace ToolsApp.Benchmarks.Candidates;

public sealed class SimdNearestColorFinder
{
    private readonly NamedColor[] _colors;
    private readonly int[] _r;
    private readonly int[] _g;
    private readonly int[] _b;

    public SimdNearestColorFinder(IEnumerable<NamedColor> colors)
    {
        _colors = [.. colors];
        _r = new int[_colors.Length];
        _g = new int[_colors.Length];
        _b = new int[_colors.Length];

        for (var i = 0; i < _colors.Length; i++)
        {
            var rgb = ColorMath.ParseHex(_colors[i].Hex);
            (_r[i], _g[i], _b[i]) = (rgb.R, rgb.G, rgb.B);
        }
    }

    public ColorMatch FindNearest(string hex)
    {
        if (_colors.Length == 0)
        {
            throw new InvalidOperationException("There are no named colors to search.");
        }

        var target = ColorMath.ParseHex(hex);
        var (bestIndex, bestDistance) = ArgMin(target);
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

    private (int Index, int Distance) ArgMin(Rgb target)
    {
        var n = _r.Length;
        var width = Vector<int>.Count;
        var best = int.MaxValue;
        var bestIndex = 0;
        var i = 0;

        if (n >= width)
        {
            // Lane j of the block starting at i is candidate i + j.
            Span<int> lanes = stackalloc int[width];
            for (var j = 0; j < width; j++)
            {
                lanes[j] = j;
            }

            var offsets = new Vector<int>(lanes);
            var (tr, tg, tb) = (new Vector<int>(target.R), new Vector<int>(target.G), new Vector<int>(target.B));
            var bestDistances = new Vector<int>(int.MaxValue);
            var bestIndexes = Vector<int>.Zero;

            for (; i + width <= n; i += width)
            {
                var dr = new Vector<int>(_r, i) - tr;
                var dg = new Vector<int>(_g, i) - tg;
                var db = new Vector<int>(_b, i) - tb;
                var distances = (dr * dr) + (dg * dg) + (db * db);

                // Strictly less than: within a lane, an equal later distance never replaces an earlier one.
                var less = Vector.LessThan(distances, bestDistances);
                bestDistances = Vector.ConditionalSelect(less, distances, bestDistances);
                bestIndexes = Vector.ConditionalSelect(less, offsets + new Vector<int>(i), bestIndexes);
            }

            // Across lanes: smallest distance wins, and a tie goes to the smaller index.
            for (var j = 0; j < width; j++)
            {
                if (bestDistances[j] < best || (bestDistances[j] == best && bestIndexes[j] < bestIndex))
                {
                    (best, bestIndex) = (bestDistances[j], bestIndexes[j]);
                }
            }
        }

        // The tail has the largest indexes, so strictly less than keeps any earlier winner on a tie.
        for (; i < n; i++)
        {
            var (dr, dg, db) = (_r[i] - target.R, _g[i] - target.G, _b[i] - target.B);
            var distance = (dr * dr) + (dg * dg) + (db * db);
            if (distance < best)
            {
                (best, bestIndex) = (distance, i);
            }
        }

        return (bestIndex, best);
    }
}
