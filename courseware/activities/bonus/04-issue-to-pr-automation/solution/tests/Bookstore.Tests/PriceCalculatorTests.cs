using Bookstore.Domain;

namespace Bookstore.Tests;

public class PriceCalculatorTests
{
    private readonly PriceCalculator _calculator = new();

    private static Book BookAt(decimal price) => new() { Title = "Test", Isbn = "978-1-23456-001-0", Price = price };

    [Fact]
    public void Calculate_adds_tax_to_the_subtotal()
    {
        var quote = _calculator.Calculate([new QuoteLine(BookAt(10m), 2)]);

        Assert.Equal(new Quote(Subtotal: 20m, Discount: 0m, Tax: 1.20m, Total: 21.20m), quote);
    }

    [Fact]
    public void Calculate_gives_bulk_discount_at_ten_copies()
    {
        var quote = _calculator.Calculate([new QuoteLine(BookAt(10m), 10)]);

        Assert.Equal(10m, quote.Discount);
        Assert.Equal(95.40m, quote.Total);
    }

    [Theory]
    [InlineData("READMORE", 60, 5)]
    [InlineData("readmore", 60, 5)]
    [InlineData("READMORE", 40, 0)]
    [InlineData("STUDENT10", 40, 4)]
    public void Calculate_applies_coupons(string coupon, int price, int expectedDiscount)
    {
        var quote = _calculator.Calculate([new QuoteLine(BookAt(price), 1)], coupon);

        Assert.Equal(expectedDiscount, quote.Discount);
    }

    [Fact]
    public void Calculate_rejects_unknown_coupons() =>
        Assert.Throws<ArgumentException>(() => _calculator.Calculate([new QuoteLine(BookAt(10m), 1)], "FREEBOOKS"));
}
