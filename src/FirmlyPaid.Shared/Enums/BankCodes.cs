namespace FirmlyPaid.Shared.Enums;

/// <summary>
/// Bank codes are strings because the real sponsor bank list grows without a code change.
/// These four are the banks the simulator knows about in phase 0.
/// </summary>
public static class BankCodes
{
    public const string Absa = "ABSA";
    public const string Fnb = "FNB";
    public const string Discovery = "DISCOVERY";
    public const string TymeBank = "TYMEBANK";

    public static readonly IReadOnlyList<string> All = [Absa, Fnb, Discovery, TymeBank];

    public static bool IsKnown(string? bankCode) =>
        bankCode is not null && All.Contains(bankCode, StringComparer.OrdinalIgnoreCase);

    public static string DisplayName(string bankCode) => bankCode.ToUpperInvariant() switch
    {
        Absa => "ABSA",
        Fnb => "FNB",
        Discovery => "Discovery Bank",
        TymeBank => "TymeBank",
        _ => bankCode,
    };
}
