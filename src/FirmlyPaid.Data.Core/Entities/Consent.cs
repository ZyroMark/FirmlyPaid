namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// POPIA consent, captured by an agent before anything biometric is stored (FR-02).
/// Kept even after withdrawal, because the record of having consented is the evidence.
/// </summary>
public class Consent
{
    public Guid ConsentId { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Which wording the customer agreed to, so we can show it again later.</summary>
    public required string ConsentTextVersion { get; set; }

    public Guid AgentId { get; set; }

    public DateTime AcceptedAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }
}
