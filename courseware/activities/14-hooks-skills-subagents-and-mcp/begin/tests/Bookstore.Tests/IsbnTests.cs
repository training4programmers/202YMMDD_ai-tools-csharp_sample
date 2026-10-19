using Bookstore.Domain;

namespace Bookstore.Tests;

public class IsbnTests
{
    [Theory]
    [InlineData("978-1-23456-001-0")]
    [InlineData("9781234560010")]
    [InlineData("978 1 23456 008 9")]
    public void IsValid_accepts_well_formed_isbn13(string isbn) =>
        Assert.True(Isbn.IsValid(isbn));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("978-1-23456-001-9")] // wrong check digit
    [InlineData("978-1-23456-001")]   // too short
    [InlineData("978-1-2345X-001-0")] // not a digit
    public void IsValid_rejects_malformed_values(string? isbn) =>
        Assert.False(Isbn.IsValid(isbn));
}
