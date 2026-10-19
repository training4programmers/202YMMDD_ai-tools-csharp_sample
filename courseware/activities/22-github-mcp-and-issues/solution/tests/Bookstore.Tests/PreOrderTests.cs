using Bookstore.Domain;

namespace Bookstore.Tests;

public class PreOrderTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    private static Book PublishedOn(DateOnly date) =>
        new() { Title = "Test", Isbn = "978-1-23456-001-0", PublishedOn = date };

    [Fact]
    public void A_book_published_after_today_is_a_pre_order() =>
        Assert.True(PublishedOn(Today.AddDays(1)).IsPreOrderOn(Today));

    [Fact]
    public void A_book_published_today_is_not_a_pre_order() =>
        Assert.False(PublishedOn(Today).IsPreOrderOn(Today));

    [Fact]
    public void A_book_published_before_today_is_not_a_pre_order() =>
        Assert.False(PublishedOn(Today.AddYears(-3)).IsPreOrderOn(Today));
}
