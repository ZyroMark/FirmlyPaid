using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>What a merchant is owed for a period, after ZYROMARK's fee.</summary>
public class Settlement
{
    public Guid SettlementId { get; set; } = Guid.NewGuid();

    public Guid MerchantId { get; set; }

    public Merchant? Merchant { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    /// <summary>Everything customers paid in the period.</summary>
    public decimal GrossAmount { get; set; }

    public decimal FeeTotal { get; set; }

    /// <summary>Gross less fees: what actually moves to the merchant.</summary>
    public decimal NetAmount { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}
