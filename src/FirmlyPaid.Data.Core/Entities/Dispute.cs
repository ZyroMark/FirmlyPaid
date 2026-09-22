using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>A customer saying a payment was not theirs or was wrong, raised from the portal.</summary>
public class Dispute
{
    public Guid DisputeId { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Payment? Payment { get; set; }

    public Guid CustomerId { get; set; }

    public required string Reason { get; set; }

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
