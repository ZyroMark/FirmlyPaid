using System.Globalization;

namespace FirmlyPaid.Shared.Money;

/// <summary>
/// Rand amounts, formatted the same way on every machine. The server's culture must not
/// decide what a customer sees on a 5 inch screen, so the format is fixed here.
/// </summary>
public static class Rand
{
    private static readonly NumberFormatInfo Format = CreateFormat();

    private static NumberFormatInfo CreateFormat()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberGroupSeparator = " ";
        format.NumberDecimalSeparator = ".";
        format.NumberDecimalDigits = 2;
        return format;
    }

    /// <summary>250 becomes "R250.00"; 3000 becomes "R3 000.00".</summary>
    public static string Format2(decimal amount) => "R" + amount.ToString("N2", Format);

    /// <summary>
    /// Rounds to whole cents, half away from zero: the rule FR-11 checks when a
    /// 1.2% merchant fee lands on half a cent.
    /// </summary>
    public static decimal RoundToCents(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
