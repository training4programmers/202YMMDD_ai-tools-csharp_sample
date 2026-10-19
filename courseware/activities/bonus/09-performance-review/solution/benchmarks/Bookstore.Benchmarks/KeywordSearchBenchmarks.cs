using BenchmarkDotNet.Attributes;
using Bookstore.Benchmarks.Candidates;
using Bookstore.Domain;

namespace Bookstore.Benchmarks;

/// <summary>
/// One type-ahead search over a 5,000-book catalog, for a common word, a two-word prefix, and an
/// author's full name. Each search object is built once, outside the measurement, the way the app
/// would hold it.
/// </summary>
[MemoryDiagnoser]
public class KeywordSearchBenchmarks
{
    private static readonly Book[] Catalog = SampleCatalog.Generate(5_000, seed: 9);

    private readonly OriginalKeywordSearch _original = new(Catalog);
    private readonly KeywordSearch _optimized = new(Catalog);
    private readonly IndexedKeywordSearch _indexed = new(Catalog);

    [Params("the", "quiet comp", "maya okafor")]
    public string Query { get; set; } = "";

    [Benchmark(Baseline = true)]
    public List<Book> Original() => _original.Search(Query);

    [Benchmark]
    public List<Book> Optimized() => _optimized.Search(Query);

    // The assistant's further idea. Measured, not adopted: see BENCHMARKS.md.
    [Benchmark]
    public List<Book> IndexedCandidate() => _indexed.Search(Query);
}
