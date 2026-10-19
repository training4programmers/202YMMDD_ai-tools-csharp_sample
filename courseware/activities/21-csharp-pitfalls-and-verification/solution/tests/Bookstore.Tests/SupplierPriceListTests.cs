using System.Globalization;
using Bookstore.Domain;

namespace Bookstore.Tests;

public class SupplierPriceListTests
{
    private const string Sample = "isbn,price\r\n9781234560010,19.49\r\n978-1-23456-005-8,42.00\r\n123456002x,25.99\r\n";

    [Fact]
    public void Parse_reads_isbn10_and_isbn13_rows_and_skips_the_header()
    {
        var result = SupplierPriceList.Parse(Sample);

        Assert.True(result.IsValid);
        Assert.Equal(
            [
                new SupplierPrice(2, "9781234560010", 19.49m),
                new SupplierPrice(3, "9781234560058", 42.00m),
                new SupplierPrice(4, "9781234560027", 25.99m),
            ],
            result.Prices);
    }

    [Fact]
    public void Parse_reads_prices_the_same_way_in_every_culture()
    {
        // In de-DE, "." is a group separator: decimal.Parse("19.49") there is 1949.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var result = SupplierPriceList.Parse("9781234560010,19.49");

            Assert.Equal(19.49m, Assert.Single(result.Prices).Price);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("9781234560010,0")]
    [InlineData("9781234560010,-5.00")]
    [InlineData("9781234560010,1000.01")]
    [InlineData("9781234560010,1,000.00")]
    [InlineData("9781234560010,19,49")]
    [InlineData("9781234560010")]
    [InlineData("9781234560011,19.49")]
    [InlineData("not-an-isbn,19.49")]
    public void Parse_rejects_bad_rows(string row)
    {
        var result = SupplierPriceList.Parse($"isbn,price\n{row}");

        Assert.False(result.IsValid);
        Assert.StartsWith("Line 2:", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_a_book_listed_twice_even_in_different_formats()
    {
        var result = SupplierPriceList.Parse("123456002X,25.99\n978-1-23456-002-7,26.99");

        Assert.Equal(["Line 2: ISBN 9781234560027 appears more than once."], result.Errors);
    }

    [Fact]
    public void Parse_reports_every_bad_row()
    {
        var result = SupplierPriceList.Parse("isbn,price\nbad,1.00\n9781234560010,19.49\nalso bad,2.00");

        Assert.Equal(2, result.Errors.Count);
    }
}
