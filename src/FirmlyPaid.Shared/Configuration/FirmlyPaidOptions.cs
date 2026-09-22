using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.Shared.Configuration;

/// <summary>
/// Every tunable rule in one place, bound from the "FirmlyPaid" configuration section and
/// validated at startup. Nothing in here is a secret; secrets come from user-secrets or
/// environment variables (rule 10.15).
/// </summary>
public sealed class FirmlyPaidOptions
{
    public const string SectionName = "FirmlyPaid";

    [Required]
    [ValidateObjectMembers]
    public EnrolmentOptions Enrolment { get; init; } = new();

    [Required]
    [ValidateObjectMembers]
    public MatchingOptions Matching { get; init; } = new();

    [Required]
    [ValidateObjectMembers]
    public PaymentOptions Payments { get; init; } = new();

    [Required]
    [ValidateObjectMembers]
    public RiskOptions Risk { get; init; } = new();

    [Required]
    [ValidateObjectMembers]
    public FeeOptions Fees { get; init; } = new();

    [Required]
    [ValidateObjectMembers]
    public AdapterOptions Adapters { get; init; } = new();
}

public sealed class EnrolmentOptions
{
    /// <summary>Two fingers, three samples each (FR-01).</summary>
    [Range(1, 6)]
    public int RequiredFingers { get; init; } = 2;

    [Range(1, 10)]
    public int SamplesPerFinger { get; init; } = 3;

    /// <summary>Samples below this quality score are rejected with a retry message.</summary>
    [Range(0, 100)]
    public int MinimumQualityScore { get; init; } = 60;

    /// <summary>A customer may link 1 to 5 bank accounts (FR-04).</summary>
    [Range(1, 10)]
    public int MaxLinkedAccounts { get; init; } = 5;

    /// <summary>The consent text version an agent must capture. Bumped when wording changes.</summary>
    [Required]
    public string ConsentTextVersion { get; init; } = "POPIA-2026-09-v1";
}

public sealed class MatchingOptions
{
    /// <summary>Similarity at or above this counts as the same finger.</summary>
    [Range(0.0, 1.0)]
    public double MatchThreshold { get; init; } = 0.85;

    /// <summary>Three match attempts per payment, then the session locks (rule 10.10).</summary>
    [Range(1, 10)]
    public int MaxMatchAttemptsPerPayment { get; init; } = 3;
}

public sealed class PaymentOptions
{
    /// <summary>A PIN is required from this amount up (FR-09: R499.99 no, R500.00 yes).</summary>
    [Range(0, 100000)]
    public decimal PinRequiredFromAmount { get; init; } = 500.00m;

    /// <summary>Pilot ceiling per payment (FR-15). Overridable per customer tier.</summary>
    [Range(0, 1000000)]
    public decimal MaxPaymentAmount { get; init; } = 3000.00m;

    /// <summary>Three wrong PINs freeze the customer (FR-09).</summary>
    [Range(1, 10)]
    public int MaxWrongPinAttempts { get; init; } = 3;

    /// <summary>How long we wait for the sponsor bank before marking the payment Failed (FR-10).</summary>
    [Range(1, 60)]
    public int BankTimeoutSeconds { get; init; } = 5;

    /// <summary>How long the "Change bank" button stays on screen when a default is set.</summary>
    [Range(1, 30)]
    public int ChangeBankPromptSeconds { get; init; } = 3;
}

public sealed class RiskOptions
{
    /// <summary>More than this many payments inside the window triggers a risk block (rule 10.10).</summary>
    [Range(1, 50)]
    public int MaxPaymentsPerWindow { get; init; } = 5;

    [Range(1, 60)]
    public int WindowMinutes { get; init; } = 2;

    /// <summary>At or above this score the customer must enter a PIN whatever the amount.</summary>
    [Range(0, 100)]
    public int RequirePinFromScore { get; init; } = 50;

    /// <summary>At or above this score the payment is blocked outright.</summary>
    [Range(0, 100)]
    public int BlockFromScore { get; init; } = 80;
}

public sealed class FeeOptions
{
    /// <summary>Merchant fee percent per payment (FR-11: 1.2% rounded to cents).</summary>
    [Range(0, 100)]
    public decimal DefaultMerchantFeePercent { get; init; } = 1.20m;

    /// <summary>Default monthly terminal rental in rand, used by the revenue report (FR-16).</summary>
    [Range(0, 100000)]
    public decimal DefaultMonthlyTerminalRental { get; init; } = 299.00m;

    /// <summary>Annual licence fee charged to a Retailer tier merchant.</summary>
    [Range(0, 10000000)]
    public decimal RetailerAnnualLicenceFee { get; init; } = 120000.00m;
}

/// <summary>
/// Which implementation stands behind each outside dependency. "Simulator" today;
/// swapping in real hardware or a real bank is a value change here plus a new adapter class.
/// </summary>
public sealed class AdapterOptions
{
    [Required] public string VeinScanner { get; init; } = AdapterNames.Simulator;
    [Required] public string VeinMatcher { get; init; } = AdapterNames.Simulator;
    [Required] public string FingerprintScanner { get; init; } = AdapterNames.Simulator;
    [Required] public string HomeAffairsVerifier { get; init; } = AdapterNames.Simulator;
    [Required] public string BankGateway { get; init; } = AdapterNames.Simulator;
    [Required] public string KeyVault { get; init; } = AdapterNames.LocalDevFile;
    [Required] public string SmsSender { get; init; } = AdapterNames.Simulator;
}

public static class AdapterNames
{
    public const string Simulator = "Simulator";
    public const string LocalDevFile = "LocalDevFile";

    // Reserved for the real adapters, so the config values are agreed up front.
    public const string HitachiFingerVein = "HitachiFingerVein";
    public const string SupremaBioMini = "SupremaBioMini";
    public const string HomeAffairsPartner = "HomeAffairsPartner";
    public const string SponsorBankPayShap = "SponsorBankPayShap";
    public const string AwsKms = "AwsKms";
    public const string SmsProvider = "SmsProvider";
}
