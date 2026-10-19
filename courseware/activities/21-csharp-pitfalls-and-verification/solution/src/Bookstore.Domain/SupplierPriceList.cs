using System.Globalization;

namespace Bookstore.Domain;

/// <summary>One row of a supplier price list: a book (by ISBN-13 digits) and its new price.</summary>
public sealed record SupplierPrice(int LineNumber, string Isbn13, decimal Price);

/// <summary>The rows a price list contains, or the reasons its lines were rejected.</summary>
public sealed record SupplierPriceListResult(IReadOnlyList<SupplierPrice> Prices, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Reads the supplier's weekly price list: one <c>isbn,price</c> pair per line after an optional
/// <c>isbn,price</c> header. ISBNs may be ISBN-10 or ISBN-13; prices always use <c>.</c> as the
/// decimal separator.
/// </summary>
public static class SupplierPriceList
{
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 1000.00m;

    public static SupplierPriceListResult Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var prices = new List<SupplierPrice>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = csv.ReplaceLineEndings("\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (i == 0 && fields[0].Equals("isbn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lineNumber = i + 1;
            if (fields.Length != 2)
            {
                errors.Add($"Line {lineNumber}: expected 'isbn,price'.");
            }
            else if (!Isbn.TryToIsbn13(fields[0], out var isbn13))
            {
                errors.Add($"Line {lineNumber}: '{fields[0]}' is not a valid ISBN-10 or ISBN-13.");
            }
            else if (!TryParsePrice(fields[1], out var price))
            {
                errors.Add($"Line {lineNumber}: '{fields[1]}' is not a price from 0.01 to 1000.00.");
            }
            else if (!seen.Add(isbn13))
            {
                errors.Add($"Line {lineNumber}: ISBN {isbn13} appears more than once.");
            }
            else
            {
                prices.Add(new SupplierPrice(lineNumber, isbn13, price));
            }
        }

        return new SupplierPriceListResult(prices, errors);
    }

    // AllowDecimalPoint only: no signs, no thousands separators, and "." whatever the server's culture.
    private static bool TryParsePrice(string text, out decimal price) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price)
        && price is >= MinPrice and <= MaxPrice;
}
