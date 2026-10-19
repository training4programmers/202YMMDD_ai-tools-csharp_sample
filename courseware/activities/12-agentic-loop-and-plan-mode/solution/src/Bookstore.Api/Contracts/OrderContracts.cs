using System.ComponentModel.DataAnnotations;
using Bookstore.Domain;

namespace Bookstore.Api.Contracts;

/// <summary>A request to place an order. Validated before the handler runs.</summary>
public sealed record OrderRequest
{
    [Required, MinLength(1), MaxLength(20)]
    public required IReadOnlyList<OrderLineRequest> Lines { get; init; }

    public string? CouponCode { get; init; }
}

/// <summary>One line of an order request: which book, and how many copies.</summary>
public sealed record OrderLineRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

/// <summary>An order as the API returns it.</summary>
public sealed record OrderDto(
    int Id,
    DateTimeOffset PlacedAt,
    string? CouponCode,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total);

/// <summary>One line of an order, with the price it sold at.</summary>
public sealed record OrderLineDto(int BookId, string Title, int Quantity, decimal UnitPrice, decimal LineTotal);

public static class OrderMappings
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.PlacedAt,
        order.CouponCode,
        [.. order.Lines.OrderBy(l => l.Id).Select(l =>
            new OrderLineDto(l.BookId, l.Book?.Title ?? "", l.Quantity, l.UnitPrice, l.LineTotal))],
        order.Subtotal,
        order.Discount,
        order.Tax,
        order.Total);
}
