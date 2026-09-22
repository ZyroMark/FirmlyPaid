using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// The sponsor bank. FirmlyPaid never holds customer money: it asks the bank to move it
/// over PayShap and records the answer. The simulator keeps fake balances so tests can
/// prove money actually moved.
/// </summary>
public interface IBankGateway
{
    /// <summary>Asks the bank to have the customer confirm account ownership in their own banking app.</summary>
    Task<AccountConfirmationRequestResult> RequestAccountConfirmationAsync(
        AccountConfirmationRequest request,
        CancellationToken ct = default);

    /// <summary>Polls the confirmation state, for cases where the bank callback has not arrived.</summary>
    Task<AccountConfirmationOutcome> GetConfirmationStatusAsync(
        string confirmationReference,
        CancellationToken ct = default);

    /// <summary>Instructs a PayShap payment. The idempotency key must make retries safe (rule 10.11).</summary>
    Task<BankPaymentResult> PayAsync(BankPaymentRequest request, CancellationToken ct = default);

    Task<BankRefundResult> RefundAsync(BankRefundRequest request, CancellationToken ct = default);
}

/// <param name="AccountNumber">Only ever passed through; FirmlyPaid stores a token, not the number.</param>
public sealed record AccountConfirmationRequest(
    string BankCode,
    string AccountNumber,
    string CustomerFullName,
    string CellphoneNumber);

/// <param name="AccountToken">The bank's opaque handle for the account. Stored encrypted.</param>
public sealed record AccountConfirmationRequestResult(
    string ConfirmationReference,
    string AccountToken,
    string AccountLast4,
    AccountConfirmationOutcome Outcome);

/// <param name="IdempotencyKey">Same key, same payment: the bank must not debit twice.</param>
public sealed record BankPaymentRequest(
    string BankCode,
    string PayerAccountToken,
    string PayeeAccountToken,
    decimal Amount,
    string MerchantReference,
    string IdempotencyKey);

/// <param name="BankReference">The bank's reference, printed on the customer receipt.</param>
public sealed record BankPaymentResult(
    BankPaymentOutcome Outcome,
    string? BankReference,
    string? DeclineReason)
{
    public bool IsApproved => Outcome == BankPaymentOutcome.Approved;
}

public sealed record BankRefundRequest(
    string BankCode,
    string OriginalBankReference,
    decimal Amount,
    string Reason,
    string IdempotencyKey);

public sealed record BankRefundResult(bool Succeeded, string? BankReference, string? FailureReason);
