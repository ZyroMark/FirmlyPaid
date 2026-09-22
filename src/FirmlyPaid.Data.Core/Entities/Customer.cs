using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// A person who can pay with a finger. The ID number itself is never here: only a salted
/// hash for duplicate checks and the digits 7 to 10 bucket used at checkout (rule 10.4).
/// </summary>
public class Customer
{
    public Guid CustomerId { get; set; } = Guid.NewGuid();

    public required string FullName { get; set; }

    /// <summary>Keyed hash of the 13 digit ID number. Unique: one profile per person.</summary>
    public required string IdNumberHash { get; set; }

    /// <summary>The SSSS group, 0 to 9999. Narrows a match to one bucket (FR-07).</summary>
    public int IdDigits7to10Bucket { get; set; }

    public required string CellphoneNumber { get; set; }

    /// <summary>Argon2id hash. Null until the agent finishes enrolment.</summary>
    public string? PinHash { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public CustomerTier Tier { get; set; } = CustomerTier.Pilot;

    /// <summary>Set for a customer whose limit differs from their tier. Null means use the tier.</summary>
    public decimal? PaymentLimitOverride { get; set; }

    public Guid? DefaultLinkedAccountId { get; set; }

    /// <summary>The only link between a person and their vault templates. Random, not derived.</summary>
    public Guid TemplateOwnerId { get; set; } = Guid.NewGuid();

    /// <summary>Three wrong PINs freeze the customer (FR-09). Reset on a correct PIN.</summary>
    public int FailedPinAttempts { get; set; }

    public DateTime? FrozenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Consent> Consents { get; set; } = [];

    public ICollection<LinkedAccount> LinkedAccounts { get; set; } = [];
}
