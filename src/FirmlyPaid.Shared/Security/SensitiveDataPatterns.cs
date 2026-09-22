using System.Text.RegularExpressions;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Patterns for data that must never reach a log, an exception message or an audit row
/// (rule 10.5). The log-scanning test in step 11 runs these over captured output.
/// </summary>
public static partial class SensitiveDataPatterns
{
    /// <summary>13 consecutive digits: a full South African ID number.</summary>
    [GeneratedRegex(@"(?<!\d)\d{13}(?!\d)", RegexOptions.CultureInvariant)]
    public static partial Regex IdNumber();

    /// <summary>9 to 12 consecutive digits: a bank account number.</summary>
    [GeneratedRegex(@"(?<!\d)\d{9,12}(?!\d)", RegexOptions.CultureInvariant)]
    public static partial Regex AccountNumber();

    /// <summary>A labelled PIN, ID digits or template value, however it is spelled.</summary>
    [GeneratedRegex(
        @"\b(pin|pin_?code|id_?digits|digits7to10|template|account_?number|account_?token)\b\s*[:=]\s*""?[^\s"",}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex LabelledSecret();

    public static readonly IReadOnlyList<Regex> All = [IdNumber(), AccountNumber(), LabelledSecret()];

    /// <summary>Returns the first pattern a piece of text trips, or null when the text is clean.</summary>
    public static string? FindViolation(string text)
    {
        foreach (var pattern in All)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }
}
