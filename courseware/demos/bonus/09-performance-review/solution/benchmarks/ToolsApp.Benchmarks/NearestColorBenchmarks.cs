using BenchmarkDotNet.Attributes;
using ToolsApp.Benchmarks.Candidates;
using ToolsApp.Domain;

namespace ToolsApp.Benchmarks;

/// <summary>
/// Names 100 random colors against the 1,000 built-in named colors: the shape of a "name every color
/// in this image's palette" request. Each finder is built once, outside the measurement, the way the
/// app would hold it.
/// </summary>
[MemoryDiagnoser]
public class NearestColorBenchmarks
{
    private readonly OriginalNearestColorFinder _original = new(NamedColors.All);
    private readonly NearestColorFinder _optimized = new(NamedColors.All);
    private readonly UnsafeNearestColorFinder _unsafe = new(NamedColors.All);
    private readonly SimdNearestColorFinder _simd = new(NamedColors.All);
    private string[] _queries = [];

    [GlobalSetup]
    public void Setup()
    {
        // A fixed seed, so every run measures exactly the same work.
        var random = new Random(21);
        _queries = [.. Enumerable.Range(0, 100).Select(_ => RandomHex(random))];
    }

    [Benchmark(Baseline = true)]
    public List<ColorMatch> Original() => _original.FindNearest(_queries);

    [Benchmark]
    public List<ColorMatch> Optimized() => _optimized.FindNearest(_queries);

    // Candidates the assistant proposed after the optimization. Measured, not shipped: see BENCHMARKS.md.
    [Benchmark]
    public List<ColorMatch> UnsafeCandidate() => _unsafe.FindNearest(_queries);

    [Benchmark]
    public List<ColorMatch> SimdCandidate() => _simd.FindNearest(_queries);

    private static string RandomHex(Random random) =>
        ColorMath.ToHex(new Rgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
}
