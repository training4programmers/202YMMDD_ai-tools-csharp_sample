using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bookstore.Mcp;

/// <summary>
/// The tools this server offers. The model reads each <see cref="DescriptionAttribute"/> to decide when to call
/// a tool and what to pass, so write them for the model, not for a human reader.
/// </summary>
[McpServerToolType]
public sealed class BookTools(IBookCatalog catalog)
{
    public const int MaxResults = 25;

    [McpServerTool(Name = "find_books", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds books in the Bookstore catalog whose title, author name, or ISBN contains the query text " +
                 "(case-insensitive). Returns at most 25 matches with id, title, author, ISBN, price, and stock.")]
    public async Task<IReadOnlyList<BookMatch>> FindBooksAsync(
        [Description("Text to look for, for example 'compiler', 'Okafor', or '978-1-23456'.")] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("query must not be empty.");
        }

        return await catalog.SearchAsync(query.Trim(), MaxResults, cancellationToken);
    }
}
