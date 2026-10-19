namespace Bookstore.Domain;

/// <summary>Validation and formatting for ISBN-13 identifiers.</summary>
public static class Isbn
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a well-formed ISBN-13:
    /// 13 digits (hyphens and spaces ignored) with a 978 or 979 prefix, whose last digit is the correct
    /// check digit.
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

        if (!digits.StartsWith("978") && !digits.StartsWith("979"))
        {
            return false;
        }

        return digits[12] - '0' == CheckDigit(digits.AsSpan(0, 12));
    }

    /// <summary>Strips hyphens and spaces, leaving only the characters that carry meaning.</summary>
    public static string Normalize(string value) => value.Trim('-', ' ');

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
