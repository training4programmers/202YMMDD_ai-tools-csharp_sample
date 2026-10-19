namespace Bookstore.Domain;

/// <summary>A title in the catalog, with its price and the copies on hand.</summary>
public sealed class Book
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>The ISBN-13, stored with or without hyphens.</summary>
    public required string Isbn { get; set; }

    public decimal Price { get; set; }

    /// <summary>Copies currently on the shelf.</summary>
    public int Stock { get; set; }

    public Genre Genre { get; set; }

    public DateOnly PublishedOn { get; set; }

    public int AuthorId { get; set; }

    public Author? Author { get; set; }

    /// <summary>Takes <paramref name="quantity"/> copies off the shelf.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">Fewer than <paramref name="quantity"/> copies are in stock.</exception>
    public void RemoveStock(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (quantity > Stock)
        {
            throw new InvalidOperationException(
                $"Only {Stock} copies of '{Title}' are in stock; {quantity} were requested.");
        }

        Stock -= quantity;
    }
}
