using ToolsApp.Benchmarks.Candidates;
using ToolsApp.Domain;

namespace ToolsApp.Tests;

/// <summary>
/// The optimization must not change a single answer. These tests run the original AI-generated finder
/// (linked from the benchmark project) and the optimized one side by side on the same inputs.
/// </summary>
public class NearestColorFinderEquivalenceTests
{
    private readonly OriginalNearestColorFinder _original = new(NamedColors.All);
    private readonly NearestColorFinder _optimized = new(NamedColors.All);

    // Every named color itself (distance 0), 2,000 random colors, the corners of the RGB cube, and a
    // known tie, in long, short, and lower-case forms.
    private static List<string> Queries()
    {
        var random = new Random(2026);

        return
        [
            .. NamedColors.All.Select(c => c.Hex),
            .. Enumerable.Range(0, 2000).Select(_ => $"#{random.Next(0x1000000):X6}"),
            "#000", "#FFF", "#F00", "#0F0", "#00F", "ff00ff", "#000C0C",
        ];
    }

    [Fact]
    public void Optimized_finder_matches_the_original_on_every_query()
    {
        var queries = Queries();

        Assert.Equal(_original.FindNearest(queries), _optimized.FindNearest(queries));
    }

    [Fact]
    public void Optimized_finder_matches_the_original_one_query_at_a_time()
    {
        foreach (var hex in Queries())
        {
            Assert.Equal(_original.FindNearest(hex), _optimized.FindNearest(hex));
        }
    }

    [Fact]
    public void Benchmark_candidates_match_the_original_too()
    {
        // A candidate that returned different answers would make its benchmark numbers meaningless.
        var queries = Queries();
        var expected = _original.FindNearest(queries);

        Assert.Equal(expected, new UnsafeNearestColorFinder(NamedColors.All).FindNearest(queries));
        Assert.Equal(expected, new SimdNearestColorFinder(NamedColors.All).FindNearest(queries));
    }

    [Fact]
    public void Both_reject_malformed_hex_the_same_way()
    {
        Assert.Throws<FormatException>(() => _original.FindNearest("#GG0000"));
        Assert.Throws<FormatException>(() => _optimized.FindNearest("#GG0000"));
    }

    [Fact]
    public void Both_fail_when_there_are_no_colors()
    {
        Assert.Throws<InvalidOperationException>(() => new OriginalNearestColorFinder([]).FindNearest("#FFFFFF"));
        Assert.Throws<InvalidOperationException>(() => new NearestColorFinder([]).FindNearest("#FFFFFF"));
    }

    [Fact]
    public void Optimized_finder_rejects_a_malformed_color_list_when_it_is_created() =>
        // A deliberate, reviewed difference: the original only failed on the first lookup.
        Assert.Throws<FormatException>(() => new NearestColorFinder([new NamedColor("Broken", "#GG0000")]));
}
