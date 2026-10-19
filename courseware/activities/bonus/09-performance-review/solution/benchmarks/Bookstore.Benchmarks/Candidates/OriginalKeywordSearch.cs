// The AI-generated search exactly as it was before the optimization, kept for two jobs: the
// benchmark baseline, and the reference the equivalence tests compare the new search against
// (tests/Bookstore.Tests links this file). Don't "fix" it; it's evidence.
using Bookstore.Domain;

namespace Bookstore.Benchmarks.Candidates;

/// <summary>
/// Type-ahead search over a catalog snapshot. A book matches when every word of the query is the start
/// of a word in its title or its author's name, ignoring case. Results are sorted by title.
/// </summary>
public class OriginalKeywordSearch
{
    private readonly IEnumerable<Book> _books;

    public OriginalKeywordSearch(IEnumerable<Book> books)
    {
        _books = books;
    }

    public List<Book> Search(string query)
    {
        var keywords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (keywords.Count == 0)
        {
            return new List<Book>();
        }

        var results = new List<Book>();
        foreach (var book in _books.ToList())
        {
            // Search the title and the author together, so "okafor quiet" finds The Quiet Compiler.
            var text = (book.Title + " " + (book.Author?.Name ?? "")).ToLowerInvariant();
            var words = text.Split(' ').ToList();

            var matched = 0;
            foreach (var keyword in keywords)
            {
                if (words.Any(w => w.StartsWith(keyword, StringComparison.Ordinal)))
                {
                    matched++;
                }
            }

            if (matched == keywords.Count)
            {
                results.Add(book);
            }
        }

        return results.OrderBy(b => b.Title).ThenBy(b => b.Id).ToList();
    }
}
