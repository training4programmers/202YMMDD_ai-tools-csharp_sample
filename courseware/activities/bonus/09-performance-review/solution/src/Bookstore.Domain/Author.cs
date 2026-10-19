namespace Bookstore.Domain;

/// <summary>A person who has written one or more books in the catalog.</summary>
public sealed class Author
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public List<Book> Books { get; set; } = [];
}
