using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>A retailer or small business that accepts FirmlyPaid.</summary>
public class Merchant
{
    public Guid MerchantId { get; set; } = Guid.NewGuid();

    public required string TradingName { get; set; }

    public required string RegistrationNumber { get; set; }

    /// <summary>ZYROMARK's cut of each payment. Default 1.20 percent (FR-11).</summary>
    public decimal FeePercent { get; set; } = 1.20m;

    public byte[]? SettlementAccountTokenCiphertext { get; set; }

    public string? SettlementAccountTokenKeyVersion { get; set; }

    public MerchantTier Tier { get; set; } = MerchantTier.Small;

    /// <summary>Licence income for the revenue report (FR-16). Zero for a small merchant.</summary>
    public decimal AnnualLicenceFee { get; set; }

    public MerchantStatus Status { get; set; } = MerchantStatus.Active;

    public DateTime CreatedAt { get; set; }

    public ICollection<Store> Stores { get; set; } = [];
}
