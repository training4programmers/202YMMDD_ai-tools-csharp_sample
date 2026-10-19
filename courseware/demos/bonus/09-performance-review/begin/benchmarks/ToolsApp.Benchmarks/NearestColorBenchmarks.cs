using BenchmarkDotNet.Attributes;
using ToolsApp.Domain;

namespace ToolsApp.Benchmarks;

/// <summary>
/// Names 100 random colors against the 1,000 built-in named colors: the shape of a "name every color
/// in this image's palette" request.
/// </summary>
[MemoryDiagnoser]
public class NearestColorBenchmarks
{
    private readonly NearestColorFinder _finder = new(NamedColors.All);
    private string[] _queries = [];

    [GlobalSetup]
    public void Setup()
    {
        // A fixed seed, so every run measures exactly the same work.
        var random = new Random(21);
        _queries = [.. Enumerable.Range(0, 100).Select(_ => RandomHex(random))];
    }

    [Benchmark(Baseline = true)]
    public List<ColorMatch> FindNearest100() => _finder.FindNearest(_queries);

    private static string RandomHex(Random random) =>
        ColorMath.ToHex(new Rgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
}
