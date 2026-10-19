using System.ComponentModel.DataAnnotations;
using Bookstore.Domain;

namespace Bookstore.Api.Contracts;

/// <summary>A book as the API returns it.</summary>
public sealed record BookDto(
    int Id,
    string Title,
    string Isbn,
    decimal Price,
    int Stock,
    Genre Genre,
    DateOnly PublishedOn,
    int AuthorId,
    string AuthorName);

/// <summary>The body of a create or update request. Validated before the handler runs.</summary>
public sealed record BookRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [Required, StringLength(17, MinimumLength = 13)]
    public required string Isbn { get; init; }

    [Range(0.01, 1000.00)]
    public decimal Price { get; init; }

    [Range(0, 10_000)]
    public int Stock { get; init; }

    public Genre Genre { get; init; }

    public DateOnly PublishedOn { get; init; }

    [Range(1, int.MaxValue)]
    public int AuthorId { get; init; }
}

/// <summary>An author with a count of their books.</summary>
public sealed record AuthorDto(int Id, string Name, int BookCount);

/// <summary>A request to price an order.</summary>
public sealed record QuoteRequest
{
    [Required, MinLength(1)]
    public required IReadOnlyList<QuoteLineRequest> Lines { get; init; }

    public string? CouponCode { get; init; }
}

/// <summary>One line of a quote request: which book, and how many copies.</summary>
public sealed record QuoteLineRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public static class BookMappings
{
    public static BookDto ToDto(this Book book) => new(
        book.Id,
        book.Title,
        book.Isbn,
        book.Price,
        book.Stock,
        book.Genre,
        book.PublishedOn,
        book.AuthorId,
        book.Author?.Name ?? "");
}
