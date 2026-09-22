using FirmlyPaid.Shared.Errors;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// What counts as a PIN. Four to six digits, matching the number pad on the customer
/// touchscreen. Checked in one place so the enrolment app, the terminal and the hasher
/// cannot drift apart.
/// </summary>
public static class PinRules
{
    public const int MinLength = 4;
    public const int MaxLength = 6;

    public static bool IsValid(string? pin)
    {
        if (pin is null || pin.Length < MinLength || pin.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in pin)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Throws the plain English validation error the customer screen shows.</summary>
    public static void EnsureValid(string? pin)
    {
        if (!IsValid(pin))
        {
            throw FirmlyPaidException.Validation(
                $"A PIN must be {MinLength} to {MaxLength} digits.");
        }
    }
}
