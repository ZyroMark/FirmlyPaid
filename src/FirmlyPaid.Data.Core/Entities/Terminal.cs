using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// A physical FirmlyPaid unit. Its certificate thumbprint is how the Gateway decides
/// whether to trust a call at all (rule 10.9).
/// </summary>
public class Terminal
{
    public Guid TerminalId { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; }

    public Store? Store { get; set; }

    public required string SerialNumber { get; set; }

    /// <summary>SHA-256 thumbprint of the device certificate. Unique across the estate.</summary>
    public required string CertificateThumbprint { get; set; }

    public TerminalType Type { get; set; } = TerminalType.Standalone;

    /// <summary>Rental income for the revenue report (FR-16).</summary>
    public decimal MonthlyRental { get; set; }

    public TerminalStatus Status { get; set; } = TerminalStatus.Active;

    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
