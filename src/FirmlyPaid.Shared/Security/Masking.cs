namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Helpers for the few places personal data may be shown: masked in the admin console,
/// last 4 digits on the bank picker. Never used to build a log line.
/// </summary>
public static class Masking
{
    /// <summary>"8001015009087" becomes "800101*******".</summary>
    public static string MaskIdNumber(string idNumber) =>
        idNumber.Length <= 6 ? new string('*', idNumber.Length) : idNumber[..6] + new string('*', idNumber.Length - 6);

    /// <summary>"0821234567" becomes "082***4567".</summary>
    public static string MaskCellphone(string cellphone) =>
        cellphone.Length <= 7
            ? new string('*', cellphone.Length)
            : cellphone[..3] + new string('*', cellphone.Length - 7) + cellphone[^4..];

    public static string Last4(string accountNumber) =>
        accountNumber.Length <= 4 ? accountNumber : accountNumber[^4..];
}
