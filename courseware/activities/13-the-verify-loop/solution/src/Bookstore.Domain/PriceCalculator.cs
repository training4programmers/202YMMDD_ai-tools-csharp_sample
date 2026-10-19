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
/// <item>Coupon codes ignore case and surrounding spaces.</item>
/// <item>Sales tax of 6% applies to the discounted amount.</item>
/// </list>
/// </summary>
public sealed class PriceCalculator
{
    public const decimal TaxRate = 0.06m;
    public const int BulkQuantity = 10;
    public const decimal BulkDiscountRate = 0.10m;

    // Coupon rules live in one table, so a new coupon is one line instead of a new switch arm.
    // Codes are matched without regard to case, as the old switch did with ToUpperInvariant.
    private static readonly Dictionary<string, Func<decimal, decimal>> Coupons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["STUDENT10"] = discountedSubtotal => discountedSubtotal * 0.10m,
        ["READMORE"] = discountedSubtotal => discountedSubtotal >= 50m ? 5m : 0m,
    };

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "PriceCalculator is registered and injected as a service; callers depend on the instance API.")]
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

            // "10 or more copies": the threshold itself qualifies.
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

    private static decimal CouponDiscount(string? couponCode, decimal discountedSubtotal)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return 0m;
        }

        return Coupons.TryGetValue(couponCode.Trim(), out var rule)
            ? rule(discountedSubtotal)
            : throw new ArgumentException($"Unknown coupon code '{couponCode}'.", nameof(couponCode));
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
