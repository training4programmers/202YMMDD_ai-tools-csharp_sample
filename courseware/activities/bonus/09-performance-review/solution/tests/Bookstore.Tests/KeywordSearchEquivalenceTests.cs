using Bookstore.Benchmarks;
using Bookstore.Benchmarks.Candidates;
using Bookstore.Domain;

namespace Bookstore.Tests;

/// <summary>
/// The optimization must not change a single result. These tests run the original AI-generated search
/// (linked from the benchmark project) and the optimized one side by side on the same catalog.
/// </summary>
public class KeywordSearchEquivalenceTests
{
    private static readonly Book[] Catalog = SampleCatalog.Generate(2_000, seed: 42);

    private readonly OriginalKeywordSearch _original = new(Catalog);
    private readonly KeywordSearch _optimized = new(Catalog);
    private readonly IndexedKeywordSearch _indexed = new(Catalog);

    [Fact]
    public void Optimized_search_returns_the_same_books_in_the_same_order()
    {
        foreach (var query in Queries())
        {
            Assert.Equal(Ids(_original.Search(query)), Ids(_optimized.Search(query)));
        }
    }

    [Fact]
    public void Benchmark_candidate_returns_the_same_books_too()
    {
        // A candidate that returned different books would make its benchmark numbers meaningless.
        foreach (var query in Queries())
        {
            Assert.Equal(Ids(_original.Search(query)), Ids(_indexed.Search(query)));
        }
    }

    [Fact]
    public void Optimized_search_keeps_the_original_order_for_equal_titles()
    {
        Author author = new() { Id = 1, Name = "Ana Silva" };
        Book[] books =
        [
            new() { Id = 3, Title = "Winter Letters", Isbn = "978-1-23456-001-0", Author = author },
            new() { Id = 1, Title = "Winter Letters", Isbn = "978-1-23456-001-0", Author = author },
            new() { Id = 2, Title = "winter letters", Isbn = "978-1-23456-001-0", Author = author },
        ];

        Assert.Equal(Ids(new OriginalKeywordSearch(books).Search("winter")), Ids(new KeywordSearch(books).Search("winter")));
    }

    // Every word of every title and author, one- and two-letter prefixes (upper-cased), pairs of words,
    // and edge cases: empty, blank, no match, a repeated word, and extra spaces.
    private static List<string> Queries()
    {
        var words = Catalog
            .SelectMany(b => $"{b.Title} {b.Author?.Name}".Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return
        [
            .. words,
            .. words.Select(w => w[..1]),
            .. words.Where(w => w.Length > 1).Select(w => w[..2].ToUpperInvariant()),
            .. words.Zip(words.Skip(7), (a, b) => $"{a} {b}"),
            "", "   ", "zzz", "the the", "  maya   okafor ",
        ];
    }

    private static List<int> Ids(List<Book> books) => [.. books.Select(b => b.Id)];
}
