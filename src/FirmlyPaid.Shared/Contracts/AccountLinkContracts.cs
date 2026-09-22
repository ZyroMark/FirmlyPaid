using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>POST /customers/{id}/accounts</summary>
public sealed record LinkAccountRequest(string BankCode, string AccountNumber, string AccountNickname);

public sealed record LinkAccountResponse(
    Guid LinkedAccountId,
    LinkedAccountStatus Status,
    string AccountLast4,
    string ConfirmationReference);

/// <summary>POST /bank-callbacks/confirmations. Signed by the bank; signature checked before use.</summary>
public sealed record BankConfirmationCallback(
    string ConfirmationReference,
    AccountConfirmationOutcome Outcome,
    string Signature);

/// <summary>PUT /customers/{id}/default-account</summary>
public sealed record SetDefaultAccountRequest(Guid LinkedAccountId);

/// <summary>What a customer or terminal may see about a linked account. Never a balance (rule 10.7).</summary>
public sealed record LinkedAccountSummary(
    Guid LinkedAccountId,
    string BankCode,
    string BankDisplayName,
    string AccountNickname,
    string AccountLast4,
    bool IsDefault,
    LinkedAccountStatus Status);
