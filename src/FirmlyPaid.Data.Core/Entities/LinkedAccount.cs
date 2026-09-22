using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// A bank account the customer may pay from. The account number is never stored: the bank
/// gives us a token, and even that is encrypted at rest.
/// </summary>
public class LinkedAccount
{
    public Guid LinkedAccountId { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public required string BankCode { get; set; }

    /// <summary>What the customer calls it, for example "Salary". Shown on the bank picker.</summary>
    public required string AccountNickname { get; set; }

    /// <summary>The only part of the account number a screen may show (rule 10.7 and 10.8).</summary>
    public required string AccountLast4 { get; set; }

    public byte[]? AccountTokenCiphertext { get; set; }

    public string? AccountTokenKeyVersion { get; set; }

    /// <summary>The bank's handle for this confirmation request, used by the callback.</summary>
    public string? ConfirmationReference { get; set; }

    public LinkedAccountStatus Status { get; set; } = LinkedAccountStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? RemovedAt { get; set; }
}
