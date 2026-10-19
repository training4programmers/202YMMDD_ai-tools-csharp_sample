using Bookstore.Domain;

namespace Bookstore.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 14, 3, 0, TimeSpan.Zero);

    private readonly PriceCalculator _calculator = new();

    private static Book NewBook(int id, decimal price, int stock) =>
        new() { Id = id, Title = $"Book {id}", Isbn = "978-1-23456-001-0", Price = price, Stock = stock };

    [Fact]
    public void Place_prices_the_order_and_copies_unit_prices_onto_the_lines()
    {
        var patterns = NewBook(5, 45.00m, stock: 20);
        var cat = NewBook(7, 12.99m, stock: 30);

        var order = Order.Place([new QuoteLine(patterns, 10), new QuoteLine(cat, 1)], "readmore", _calculator, Now);

        Assert.Equal((462.99m, 50.00m, 24.78m, 437.77m), (order.Subtotal, order.Discount, order.Tax, order.Total));
        Assert.Equal("READMORE", order.CouponCode);
        Assert.Equal(Now, order.PlacedAt);
        Assert.Equal([45.00m, 12.99m], order.Lines.Select(l => l.UnitPrice));
    }

    [Fact]
    public void Place_takes_every_line_off_the_shelf()
    {
        var first = NewBook(1, 10m, stock: 5);
        var second = NewBook(2, 10m, stock: 3);

        Order.Place([new QuoteLine(first, 2), new QuoteLine(second, 3)], null, _calculator, Now);

        Assert.Equal(3, first.Stock);
        Assert.Equal(0, second.Stock);
    }

    [Fact]
    public void Place_changes_no_stock_when_any_line_is_short()
    {
        var plenty = NewBook(1, 10m, stock: 50);
        var scarce = NewBook(2, 10m, stock: 3);

        var error = Assert.Throws<InsufficientStockException>(() =>
            Order.Place([new QuoteLine(plenty, 5), new QuoteLine(scarce, 4)], null, _calculator, Now));

        Assert.Equal(new StockShortage(2, "Book 2", Requested: 4, Available: 3), Assert.Single(error.Shortages));
        Assert.Equal(50, plenty.Stock);
        Assert.Equal(3, scarce.Stock);
    }

    [Fact]
    public void Place_changes_no_stock_when_the_coupon_is_unknown()
    {
        var book = NewBook(1, 10m, stock: 5);

        var error = Assert.Throws<ArgumentException>(() =>
            Order.Place([new QuoteLine(book, 1)], "FREEBOOKS", _calculator, Now));

        Assert.Equal("couponCode", error.ParamName);
        Assert.Equal(5, book.Stock);
    }
}
