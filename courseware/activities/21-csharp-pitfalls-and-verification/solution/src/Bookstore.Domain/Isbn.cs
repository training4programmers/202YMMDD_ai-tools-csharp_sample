using System.Diagnostics.CodeAnalysis;

namespace Bookstore.Domain;

/// <summary>Validation and formatting for ISBN-13 identifiers.</summary>
public static class Isbn
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a well-formed ISBN-13:
    /// 13 digits (hyphens and spaces ignored) whose last digit is the correct check digit.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = Normalize(value);
        if (digits.Length != 13 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        return digits[12] - '0' == CheckDigit(digits.AsSpan(0, 12));
    }

    /// <summary>Strips hyphens and spaces, leaving only the characters that carry meaning.</summary>
    public static string Normalize(string value) =>
        value.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

    /// <summary>
    /// Reads an ISBN-13, or an ISBN-10 (whose check character may be <c>X</c> or <c>x</c>), and returns
    /// the ISBN-13 as 13 plain digits. Hyphens and spaces are ignored. Returns <see langword="false"/>
    /// when the value is malformed or its check digit is wrong.
    /// </summary>
    public static bool TryToIsbn13(string? value, [NotNullWhen(true)] out string? isbn13)
    {
        isbn13 = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = Normalize(value);
        if (digits.Length == 13)
        {
            if (!IsValid(digits))
            {
                return false;
            }

            isbn13 = digits;
            return true;
        }

        if (digits.Length != 10 || !IsValidIsbn10(digits))
        {
            return false;
        }

        // An ISBN-10 becomes an ISBN-13 by prefixing 978 and recomputing the check digit.
        var first12 = string.Concat("978", digits.AsSpan(0, 9));
        isbn13 = first12 + (char)('0' + CheckDigit(first12));
        return true;
    }

    // ISBN-10 weights the digits 10, 9, ..., 1, and the total must be a multiple of 11.
    // The last character can be X (or x), which stands for 10.
    private static bool IsValidIsbn10(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = digits[i];
            int value;
            if (char.IsAsciiDigit(c))
            {
                value = c - '0';
            }
            else if (i == 9 && (c is 'X' or 'x'))
            {
                value = 10;
            }
            else
            {
                return false;
            }

            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    // ISBN-13 weights the digits 1, 3, 1, 3, ... and the check digit brings the sum to a multiple of 10.
    private static int CheckDigit(ReadOnlySpan<char> first12)
    {
        var sum = 0;
        for (var i = 0; i < first12.Length; i++)
        {
            var digit = first12[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (10 - (sum % 10)) % 10;
    }
}
