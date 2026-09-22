using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>A merchant-initiated return of money, approved with a manager PIN at the till.</summary>
public class Refund
{
    public Guid RefundId { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Payment? Payment { get; set; }

    public decimal Amount { get; set; }

    public required string Reason { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public string? BankReference { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
