namespace Bookstore.Domain;

/// <summary>
/// Type-ahead search over a catalog snapshot. A book matches when every word of the query is the start
/// of a word in its title or its author's name, ignoring case. Results are sorted by title.
/// </summary>
/// <remarks>
/// The catalog is indexed once, when the search is created, so later changes to the books aren't seen.
/// Build a new <see cref="KeywordSearch"/> when the catalog changes.
/// </remarks>
public sealed class KeywordSearch
{
    private readonly Book[] _books;

    // The lower-case words of each book's title and author name, split once instead of on every search.
    private readonly string[][] _words;

    public KeywordSearch(IEnumerable<Book> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        _books = [.. books];
        _words = Array.ConvertAll(_books, book =>
            $"{book.Title} {book.Author?.Name}".ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public List<Book> Search(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var keywords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length == 0)
        {
            return [];
        }

        var results = new List<Book>();
        for (var i = 0; i < _books.Length; i++)
        {
            if (MatchesEveryKeyword(_words[i], keywords))
            {
                results.Add(_books[i]);
            }
        }

        // Same ordering as before (OrderBy is stable, and the comparer is unchanged); only the matches
        // are sorted, so this isn't the hot path.
        return [.. results.OrderBy(b => b.Title).ThenBy(b => b.Id)];
    }

    private static bool MatchesEveryKeyword(string[] words, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!StartsAnyWord(words, keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsAnyWord(string[] words, string keyword)
    {
        foreach (var word in words)
        {
            if (word.StartsWith(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
