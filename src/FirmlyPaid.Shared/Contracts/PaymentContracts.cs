using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>POST /payments/start</summary>
public sealed record StartPaymentRequest(
    Guid TerminalId,
    decimal Amount,
    string MerchantReference,
    string IdempotencyKey);

public sealed record StartPaymentResponse(Guid PaymentId, decimal Amount, string StoreName, string Prompt);

/// <summary>POST /payments/{id}/identify</summary>
public sealed record IdentifyPaymentRequest(EncryptedPayloadDto Probe, string IdDigits7To10);

/// <param name="Accounts">Shown on the customer screen only when there is more than one and no default.</param>
/// <param name="SelectedAccountId">Set when a default or single account made the choice for the customer.</param>
public sealed record IdentifyPaymentResponse(
    Guid PaymentId,
    IReadOnlyList<LinkedAccountSummary> Accounts,
    Guid? SelectedAccountId,
    bool PinRequired,
    int ChangeBankPromptSeconds);

/// <summary>POST /payments/{id}/confirm</summary>
public sealed record ConfirmPaymentRequest(Guid LinkedAccountId, EncryptedPayloadDto? EncryptedPin);

public sealed record ConfirmPaymentResponse(
    Guid PaymentId,
    PaymentStatus Status,
    string? DeclineReason,
    ReceiptDto? Receipt);

/// <summary>What prints or shows on screen after a payment. No balances, no account number.</summary>
public sealed record ReceiptDto(
    string ReceiptNumber,
    decimal Amount,
    string StoreName,
    string BankDisplayName,
    string AccountLast4,
    DateTime CompletedAtUtc);

/// <summary>POST /payments/{id}/refund</summary>
public sealed record RefundPaymentRequest(decimal Amount, string Reason);

public sealed record RefundPaymentResponse(Guid RefundId, RefundStatus Status);
