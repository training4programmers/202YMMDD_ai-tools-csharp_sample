using System.Diagnostics.CodeAnalysis;

namespace Bookstore.Domain;

/// <summary>One line of an order: a book and how many copies.</summary>
public sealed record QuoteLine(Book Book, int Quantity);

/// <summary>The priced result of an order, rounded to cents.</summary>
public sealed record Quote(decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

/// <summary>
/// Prices an order. Rules:
/// <list type="bullet">
/// <item>Any line with 10 or more copies gets 10% off that line (bulk discount).</item>
/// <item>Coupon <c>STUDENT10</c> takes 10% off the whole order after bulk discounts.</item>
/// <item>Coupon <c>READMORE</c> takes $5.00 off orders whose discounted subtotal is at least $50.00.</item>
/// <item>Sales tax of 6% applies to the discounted amount.</item>
/// </list>
/// </summary>
public sealed class PriceCalculator
{
    public const decimal TaxRate = 0.06m;
    public const int BulkQuantity = 10;
    public const decimal BulkDiscountRate = 0.10m;

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "PriceCalculator is a DI service injected into the quote endpoint. Keeping Calculate " +
            "an instance method lets it take dependencies (tax tables, coupon rules) without changing callers.")]
    public Quote Calculate(IReadOnlyList<QuoteLine> lines, string? couponCode = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var subtotal = 0m;
        var discount = 0m;

        foreach (var line in lines)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line.Quantity);

            var lineTotal = line.Book.Price * line.Quantity;
            subtotal += lineTotal;

            if (line.Quantity >= BulkQuantity)
            {
                discount += lineTotal * BulkDiscountRate;
            }
        }

        discount += CouponDiscount(couponCode, subtotal - discount);

        var taxable = subtotal - discount;
        var tax = Round(taxable * TaxRate);

        return new Quote(Round(subtotal), Round(discount), tax, Round(taxable) + tax);
    }

    private static decimal CouponDiscount(string? couponCode, decimal discountedSubtotal) =>
        couponCode?.Trim().ToUpperInvariant() switch
        {
            null or "" => 0m,
            "STUDENT10" => discountedSubtotal * 0.10m,
            "READMORE" when discountedSubtotal >= 50m => 5m,
            "READMORE" => 0m,
            _ => throw new ArgumentException($"Unknown coupon code '{couponCode}'.", nameof(couponCode)),
        };

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
