// An AI-proposed "further optimization" of KeywordSearch: an ordinal-sorted index of every word, so a
// search binary-searches the most selective keyword's prefix block and checks only those books.
// Measured in BENCHMARKS.md: faster, but not adopted (see there for why). Adapted from the recorded run
// after review: the agent's version pre-sorted the catalog by title when the index was built, which
// moves the culture-sensitive title sort from search time to construction time; this version sorts the
// matches at search time, exactly as KeywordSearch does.
using Bookstore.Domain;

namespace Bookstore.Benchmarks.Candidates;

public sealed class IndexedKeywordSearch
{
    private readonly Book[] _books;
    private readonly string[][] _words;
    private readonly string[] _sortedWords;   // every word of every book, ordinal order
    private readonly int[] _wordBook;         // parallel to _sortedWords: the book each word came from

    public IndexedKeywordSearch(IEnumerable<Book> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        _books = [.. books];
        _words = Array.ConvertAll(_books, book =>
            $"{book.Title} {book.Author?.Name}".ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var entries = new List<(string Word, int Book)>();
        for (var i = 0; i < _words.Length; i++)
        {
            foreach (var word in _words[i])
            {
                entries.Add((word, i));
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
        _sortedWords = [.. entries.Select(e => e.Word)];
        _wordBook = [.. entries.Select(e => e.Book)];
    }

    public List<Book> Search(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var keywords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length == 0)
        {
            return [];
        }

        // The smallest block of words starting with one of the keywords bounds the candidate books.
        var (bestStart, bestEnd) = (0, int.MaxValue);
        foreach (var keyword in keywords)
        {
            var start = LowerBound(keyword);
            var end = start;
            while (end < _sortedWords.Length && _sortedWords[end].StartsWith(keyword, StringComparison.Ordinal))
            {
                end++;
            }

            if (end == start)
            {
                return [];
            }

            if (end - start < bestEnd - bestStart)
            {
                (bestStart, bestEnd) = (start, end);
            }
        }

        var candidates = new HashSet<int>();
        for (var i = bestStart; i < bestEnd; i++)
        {
            candidates.Add(_wordBook[i]);
        }

        var results = new List<Book>();
        foreach (var index in candidates)
        {
            if (MatchesEveryKeyword(_words[index], keywords))
            {
                results.Add(_books[index]);
            }
        }

        return [.. results.OrderBy(b => b.Title).ThenBy(b => b.Id)];
    }

    private static bool MatchesEveryKeyword(string[] words, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var found = false;
            foreach (var word in words)
            {
                if (word.StartsWith(keyword, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    // The first index whose word is >= key in ordinal order.
    private int LowerBound(string key)
    {
        var (low, high) = (0, _sortedWords.Length);
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (string.CompareOrdinal(_sortedWords[mid], key) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
