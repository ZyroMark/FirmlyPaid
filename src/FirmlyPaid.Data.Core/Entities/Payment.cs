using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// One attempt to pay at a till. The row exists from the moment the cashier enters an
/// amount, so a decline is as traceable as an approval.
/// </summary>
public class Payment
{
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    public Guid TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public Guid MerchantId { get; set; }

    /// <summary>Carried on the row so the dashboard can group by store without a join chain.</summary>
    public Guid StoreId { get; set; }

    /// <summary>Null until the customer has been identified by finger and ID digits.</summary>
    public Guid? CustomerId { get; set; }

    public Guid? LinkedAccountId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The merchant fee ZYROMARK earns, rounded to cents (FR-11).</summary>
    public decimal FeeAmount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>An error code from ErrorCodes, never free text from an exception.</summary>
    public string? DeclineReason { get; set; }

    public string? BankReference { get; set; }

    public required string MerchantReference { get; set; }

    public int RiskScore { get; set; }

    public bool PinUsed { get; set; }

    /// <summary>Unique. A retry with the same key must never charge twice (rule 10.11).</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>Counts failed finger reads on this payment. Three ends the session (rule 10.10).</summary>
    public int MatchAttempts { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
