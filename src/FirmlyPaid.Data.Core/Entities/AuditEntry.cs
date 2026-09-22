namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// One line in the append-only audit log. Each row's Hash covers its own content plus the
/// previous row's Hash, so changing or deleting a row breaks the chain and is detectable
/// (FR-13). Details never contains personal data (rule 10.5).
/// </summary>
public class AuditEntry
{
    public Guid AuditId { get; set; } = Guid.NewGuid();

    /// <summary>Database-assigned order. The chain is verified in this order.</summary>
    public long Sequence { get; set; }

    /// <summary>Who did it: an admin user id, an agent id, a terminal serial, or "system".</summary>
    public required string Actor { get; set; }

    public required string Action { get; set; }

    public required string EntityType { get; set; }

    public required string EntityId { get; set; }

    public required string Details { get; set; }

    public required string PreviousHash { get; set; }

    public required string Hash { get; set; }

    public DateTime CreatedAt { get; set; }
}
