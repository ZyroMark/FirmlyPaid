using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// One agent-assisted sign-up in progress. It carries the person's details until the PIN
/// step completes, at which point a Customer row is created with the same TemplateOwnerId.
/// Keeping them apart means a half-finished enrolment never looks like a live customer.
/// </summary>
public class Enrolment
{
    public Guid EnrolmentId { get; set; } = Guid.NewGuid();

    /// <summary>Set only once the enrolment completes.</summary>
    public Guid? CustomerId { get; set; }

    public Guid AgentId { get; set; }

    public Guid StoreId { get; set; }

    public required string FullName { get; set; }

    public required string IdNumberHash { get; set; }

    public int IdDigits7to10Bucket { get; set; }

    public required string CellphoneNumber { get; set; }

    /// <summary>Allocated up front so vein samples can be stored before the customer exists.</summary>
    public Guid TemplateOwnerId { get; set; } = Guid.NewGuid();

    public HomeAffairsOutcome HomeAffairsResult { get; set; }

    public required string HomeAffairsReference { get; set; }

    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.InProgress;

    /// <summary>How many distinct fingers have all their samples accepted. Two are needed (FR-01).</summary>
    public int FingersCaptured { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
