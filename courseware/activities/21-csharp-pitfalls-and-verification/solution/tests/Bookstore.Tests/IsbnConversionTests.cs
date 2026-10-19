using Bookstore.Domain;

namespace Bookstore.Tests;

public class IsbnConversionTests
{
    [Theory]
    [InlineData("1234560011", "9781234560010")]
    [InlineData("123456002X", "9781234560027")]
    [InlineData("123456002x", "9781234560027")]
    [InlineData("1-23456-002-X", "9781234560027")]
    [InlineData("978-1-23456-005-8", "9781234560058")]
    [InlineData("9781234560072", "9781234560072")]
    public void TryToIsbn13_reads_isbn10_and_isbn13(string value, string expected)
    {
        Assert.True(Isbn.TryToIsbn13(value, out var isbn13));
        Assert.Equal(expected, isbn13);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234560012")]      // wrong ISBN-10 check digit
    [InlineData("12345600X1")]      // X anywhere but last
    [InlineData("9781234560011")]   // wrong ISBN-13 check digit
    [InlineData("12345")]
    public void TryToIsbn13_rejects_malformed_values(string? value) =>
        Assert.False(Isbn.TryToIsbn13(value, out _));

    [Fact]
    public void TryToIsbn13_output_is_a_valid_isbn13() =>
        Assert.True(Isbn.TryToIsbn13("123456002X", out var isbn13) && Isbn.IsValid(isbn13));
}
