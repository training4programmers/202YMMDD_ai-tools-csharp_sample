using Bookstore.Domain;

namespace Bookstore.Tests;

public class KeywordSearchTests
{
    private static readonly Author Maya = new() { Id = 1, Name = "Maya Okafor" };
    private static readonly Author Priya = new() { Id = 3, Name = "Priya Raman" };

    private static readonly Book[] Catalog =
    [
        NewBook(1, "The Quiet Compiler", Maya),
        NewBook(2, "Rivers of Glass", Maya),
        NewBook(5, "Patterns in the Noise", Priya),
        NewBook(6, "Async All the Way Down", Priya),
        NewBook(9, "Untitled Draft", author: null),
    ];

    private readonly KeywordSearch _search = new(Catalog);

    [Theory]
    [InlineData("quiet", new[] { 1 })]
    [InlineData("QUIET comp", new[] { 1 })]
    [InlineData("okafor", new[] { 2, 1 })]
    [InlineData("the", new[] { 6, 5, 1 })]
    [InlineData("priya async", new[] { 6 })]
    [InlineData("  rivers   glass ", new[] { 2 })]
    [InlineData("draft", new[] { 9 })]
    public void Search_matches_every_keyword_against_title_and_author(string query, int[] expectedIds) =>
        Assert.Equal(expectedIds, _search.Search(query).Select(b => b.Id));

    [Theory]
    [InlineData("piler")]       // inside a word, not at its start
    [InlineData("quiet glass")] // each keyword matches a different book
    [InlineData("")]
    [InlineData("   ")]
    public void Search_returns_nothing_when_a_keyword_has_no_match(string query) =>
        Assert.Empty(_search.Search(query));

    private static Book NewBook(int id, string title, Author? author) => new()
    {
        Id = id,
        Title = title,
        Isbn = "978-1-23456-001-0",
        Author = author,
        AuthorId = author?.Id ?? 0,
    };
}
