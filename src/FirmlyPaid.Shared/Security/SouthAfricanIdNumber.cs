using System.Globalization;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// South African ID number rules. Format is YYMMDD SSSS C A Z (13 digits);
/// digits 7 to 10 are the SSSS group the customer types at checkout.
/// </summary>
public static class SouthAfricanIdNumber
{
    public const int Length = 13;

    /// <summary>
    /// True when the number is 13 digits, carries a real birth date and passes the
    /// Luhn check digit. Checked before any Home Affairs call (FR-03).
    /// </summary>
    public static bool IsValid(string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length != Length)
        {
            return false;
        }

        foreach (var c in idNumber)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return HasValidBirthDate(idNumber) && HasValidCheckDigit(idNumber);
    }

    /// <summary>The SSSS group (digits 7 to 10) as an int from 0 to 9999: the matching bucket.</summary>
    public static int Bucket(string idNumber)
    {
        if (!IsValid(idNumber))
        {
            throw new ArgumentException("ID number is not valid.", nameof(idNumber));
        }

        return int.Parse(idNumber.AsSpan(6, 4), CultureInfo.InvariantCulture);
    }

    /// <summary>Validates the four digits a customer types on the terminal keypad.</summary>
    public static bool TryParseBucket(string? digits7To10, out int bucket)
    {
        bucket = 0;
        if (digits7To10 is null || digits7To10.Length != 4)
        {
            return false;
        }

        return int.TryParse(digits7To10, NumberStyles.None, CultureInfo.InvariantCulture, out bucket);
    }

    private static bool HasValidBirthDate(string idNumber) =>
        DateTime.TryParseExact(
            idNumber[..6],
            "yyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    /// <summary>Luhn check over the first 12 digits, compared with digit 13.</summary>
    private static bool HasValidCheckDigit(string idNumber)
    {
        var sum = 0;

        // Luhn doubles every second digit starting from the rightmost digit of the
        // 12 digit payload, so the alternation starts switched on.
        var doubleIt = true;

        for (var i = Length - 2; i >= 0; i--)
        {
            var digit = idNumber[i] - '0';

            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        var expected = (10 - (sum % 10)) % 10;
        return expected == idNumber[Length - 1] - '0';
    }

    /// <summary>Appends the correct check digit to 12 digits. Used by seed data and tests.</summary>
    public static string WithCheckDigit(string first12Digits)
    {
        if (first12Digits.Length != Length - 1)
        {
            throw new ArgumentException("Expected 12 digits.", nameof(first12Digits));
        }

        for (var candidate = 0; candidate <= 9; candidate++)
        {
            var attempt = first12Digits + candidate.ToString(CultureInfo.InvariantCulture);
            if (HasValidCheckDigit(attempt))
            {
                return attempt;
            }
        }

        throw new InvalidOperationException("No valid check digit found.");
    }
}
