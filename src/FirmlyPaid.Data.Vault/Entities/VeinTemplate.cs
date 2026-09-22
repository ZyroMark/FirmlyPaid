using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Vault.Entities;

/// <summary>
/// An encrypted finger vein template. Nothing here names a person: the only link out is
/// TemplateOwnerId, a random value held on the customer row in the other database.
/// </summary>
public class VeinTemplate
{
    public Guid TemplateId { get; set; } = Guid.NewGuid();

    /// <summary>Random handle for the person. Never derived from a name or an ID number.</summary>
    public Guid TemplateOwnerId { get; set; }

    public FingerPosition FingerPosition { get; set; }

    /// <summary>Ciphertext only. A raw image never reaches this database (rule 10.1).</summary>
    public required byte[] EncryptedTemplate { get; set; }

    /// <summary>Which key encrypted it, so keys can rotate without orphaning old rows.</summary>
    public required string KeyVersion { get; set; }

    /// <summary>
    /// The per-customer transform applied before storage (rule 10.3). If a template ever
    /// leaks, the seed is revoked and the customer re-enrols with a fresh transform, so
    /// the leaked copy matches nothing.
    /// </summary>
    public Guid TransformSeedId { get; set; }

    public int QualityScore { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the template is retired. Revoked rows are never matched against.</summary>
    public DateTime? RevokedAt { get; set; }
}
