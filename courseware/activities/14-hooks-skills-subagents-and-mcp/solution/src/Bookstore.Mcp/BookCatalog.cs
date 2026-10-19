using Microsoft.Data.Sqlite;
using ModelContextProtocol;

namespace Bookstore.Mcp;

/// <summary>A catalog entry, as the MCP tools return it.</summary>
public sealed record BookMatch(int Id, string Title, string Author, string Isbn, decimal Price, int Stock);

/// <summary>Read-only access to the books the Bookstore API stores.</summary>
public interface IBookCatalog
{
    /// <summary>Books whose title, author, or ISBN contains <paramref name="text"/> (case-insensitive).</summary>
    Task<IReadOnlyList<BookMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken);

    /// <summary>The books with these ISBNs, given as 13 digits without hyphens or spaces.</summary>
    Task<IReadOnlyList<BookMatch>> FindByIsbnsAsync(IReadOnlyCollection<string> normalizedIsbns, CancellationToken cancellationToken);
}

/// <summary>
/// Queries the API's SQLite file directly. The tables are the ones EF Core creates for
/// <c>BookstoreDbContext</c>: <c>Books</c> (price stored as REAL) and <c>Authors</c>.
/// </summary>
public sealed class SqliteBookCatalog(string connectionString) : IBookCatalog
{
    private const string SelectBooks = """
        SELECT b.Id, b.Title, a.Name, b.Isbn, b.Price, b.Stock
        FROM Books AS b
        JOIN Authors AS a ON a.Id = b.AuthorId
        """;

    // Stored ISBNs may contain hyphens or spaces, so compare with those removed.
    private const string NormalizedIsbn = "REPLACE(REPLACE(b.Isbn, '-', ''), ' ', '')";

    public async Task<IReadOnlyList<BookMatch>> SearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectBooks}
            WHERE b.Title LIKE $pattern ESCAPE '\'
               OR a.Name LIKE $pattern ESCAPE '\'
               OR b.Isbn LIKE $pattern ESCAPE '\'
               OR {NormalizedIsbn} LIKE $pattern ESCAPE '\'
            ORDER BY b.Title
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$pattern", $"%{EscapeLike(text)}%");
        command.Parameters.AddWithValue("$limit", limit);

        return await ReadMatchesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<BookMatch>> FindByIsbnsAsync(
        IReadOnlyCollection<string> normalizedIsbns, CancellationToken cancellationToken)
    {
        if (normalizedIsbns.Count == 0)
        {
            return [];
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // One parameter per ISBN: values never become part of the SQL text.
        var names = new List<string>();
        foreach (var isbn in normalizedIsbns)
        {
            var name = $"$isbn{names.Count}";
            command.Parameters.AddWithValue(name, isbn);
            names.Add(name);
        }

        command.CommandText = $"""
            {SelectBooks}
            WHERE {NormalizedIsbn} IN ({string.Join(", ", names)})
            """;

        return await ReadMatchesAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<BookMatch>> ReadMatchesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var matches = new List<BookMatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new BookMatch(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                (decimal)reader.GetDouble(4), // EF stores decimal prices as REAL on SQLite
                reader.GetInt32(5)));
        }

        return matches;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (!File.Exists(path))
        {
            // McpException messages reach the model, so say exactly how to fix it.
            throw new McpException(
                $"No Bookstore database at '{path}'. Run the API once (dotnet run --project src/Bookstore.Api) to create it.");
        }

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    // '%' and '_' are LIKE wildcards; treat them as literal characters in a search.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
