using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// One rule firing during a payment. Kept whatever the outcome, so an admin can see why
/// a customer was asked for a PIN or blocked.
/// </summary>
public class RiskEvent
{
    public Guid RiskEventId { get; set; } = Guid.NewGuid();

    public Guid? CustomerId { get; set; }

    public Guid? TerminalId { get; set; }

    public Guid? PaymentId { get; set; }

    /// <summary>The rule name, for example "FivePaymentsInTwoMinutes".</summary>
    public required string Rule { get; set; }

    public int Score { get; set; }

    public RiskAction Action { get; set; }

    public DateTime CreatedAt { get; set; }
}
