using System.Collections.Concurrent;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.Bank;

/// <summary>
/// Stands in for the sponsor bank and PayShap. Knows the four pilot banks, confirms account
/// ownership the way a banking app would, and moves money in the fake ledger.
/// </summary>
/// <remarks>
/// The behaviour that matters most here is idempotency. A terminal that times out and
/// retries sends the same key again, and the customer must be debited once (rule 10.11).
/// The real bank promises the same thing, so the simulator has to as well or the retry
/// path would only ever be tested in production.
/// </remarks>
public sealed class SimulatedBankGateway(
    FakeBankLedger ledger,
    SimulatorControlState control,
    IClock clock,
    ILogger<SimulatedBankGateway> logger) : IBankGateway
{
    private readonly ConcurrentDictionary<string, BankPaymentResult> _paymentsByIdempotencyKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingConfirmation> _confirmations = new(StringComparer.Ordinal);

    private sealed record PendingConfirmation(
        string AccountToken,
        DateTime RequestedAtUtc,
        DateTime AnswersAtUtc,
        AccountConfirmationMode Mode);

    public Task<AccountConfirmationRequestResult> RequestAccountConfirmationAsync(
        AccountConfirmationRequest request,
        CancellationToken ct = default)
    {
        if (!BankCodes.IsKnown(request.BankCode))
        {
            throw new ArgumentException($"The fake bank does not know '{request.BankCode}'.", nameof(request));
        }

        var last4 = Masking.Last4(request.AccountNumber);

        // The bank opens the account and hands back a token. From here on FirmlyPaid
        // holds the token, never the account number.
        var accountToken = ledger.OpenAccount(request.BankCode, last4);

        var reference = $"CONF-{Guid.NewGuid():N}"[..17];
        var mode = control.AccountConfirmation;
        var now = clock.UtcNow;

        _confirmations[reference] = new PendingConfirmation(
            accountToken,
            now,
            now.Add(control.AccountConfirmationDelay),
            mode);

        logger.LogInformation(
            "Fake bank {BankCode} asked the customer to confirm an account, reference {Reference}",
            request.BankCode,
            reference);

        // The answer comes later, from the customer's own banking app.
        return Task.FromResult(new AccountConfirmationRequestResult(
            reference,
            accountToken,
            last4,
            AccountConfirmationOutcome.Pending));
    }

    public Task<AccountConfirmationOutcome> GetConfirmationStatusAsync(
        string confirmationReference,
        CancellationToken ct = default)
    {
        if (!_confirmations.TryGetValue(confirmationReference, out var pending))
        {
            throw new KeyNotFoundException("That confirmation reference is not known to the fake bank.");
        }

        if (pending.Mode == AccountConfirmationMode.NeverAnswer)
        {
            return Task.FromResult(AccountConfirmationOutcome.Pending);
        }

        // Nothing happens until the customer has had time to open their app.
        if (clock.UtcNow < pending.AnswersAtUtc)
        {
            return Task.FromResult(AccountConfirmationOutcome.Pending);
        }

        var outcome = pending.Mode == AccountConfirmationMode.Reject
            ? AccountConfirmationOutcome.Rejected
            : AccountConfirmationOutcome.Confirmed;

        return Task.FromResult(outcome);
    }

    public async Task<BankPaymentResult> PayAsync(BankPaymentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Every payment must carry an idempotency key.", nameof(request));
        }

        // The whole point of the key: the same instruction twice is one debit.
        if (_paymentsByIdempotencyKey.TryGetValue(request.IdempotencyKey, out var alreadyDone))
        {
            logger.LogInformation("Fake bank replayed the answer for an idempotency key it had already seen");
            return alreadyDone;
        }

        var mode = control.BankPayment;

        if (mode == BankPaymentMode.Timeout)
        {
            // Never answers. The caller's own timeout decides when to give up, and the
            // key is deliberately not recorded, so a later retry is a fresh attempt.
            logger.LogInformation("Fake bank is not answering, to exercise the timeout path");
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }

        var outcome = mode switch
        {
            BankPaymentMode.AlwaysApprove => ForceApprove(request),
            BankPaymentMode.InsufficientFunds => BankPaymentOutcome.InsufficientFunds,
            BankPaymentMode.AlwaysDecline => BankPaymentOutcome.Declined,
            _ => ledger.Transfer(request.PayerAccountToken, request.PayeeAccountToken, request.Amount),
        };

        var result = outcome == BankPaymentOutcome.Approved
            ? new BankPaymentResult(outcome, $"PS-{Guid.NewGuid():N}"[..15], null)
            : new BankPaymentResult(outcome, null, DescribeDecline(outcome));

        _paymentsByIdempotencyKey[request.IdempotencyKey] = result;

        logger.LogInformation("Fake bank answered {Outcome} for R{Amount}", outcome, request.Amount);

        return result;
    }

    public Task<BankRefundResult> RefundAsync(BankRefundRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalBankReference))
        {
            return Task.FromResult(new BankRefundResult(false, null, "The original payment reference is missing."));
        }

        logger.LogInformation("Fake bank refunded R{Amount}", request.Amount);

        return Task.FromResult(new BankRefundResult(true, $"PS-RF-{Guid.NewGuid():N}"[..18], null));
    }

    /// <summary>
    /// Approves whatever the balance says, for a demo where the money is beside the point.
    /// The ledger still moves if it can, so the books stay consistent.
    /// </summary>
    private BankPaymentOutcome ForceApprove(BankPaymentRequest request)
    {
        ledger.Transfer(request.PayerAccountToken, request.PayeeAccountToken, request.Amount);
        return BankPaymentOutcome.Approved;
    }

    /// <summary>Plain English for a cashier, with nothing about the customer's money in it.</summary>
    private static string DescribeDecline(BankPaymentOutcome outcome) => outcome switch
    {
        BankPaymentOutcome.InsufficientFunds => "The bank did not approve this payment.",
        BankPaymentOutcome.Declined => "The bank declined this payment.",
        _ => "The bank did not complete this payment.",
    };

    /// <summary>
    /// Reverses a refunded payment in the ledger. Separate from RefundAsync because the
    /// payment service decides which accounts were involved, not the bank adapter.
    /// </summary>
    public void ApplyRefundToLedger(string payerToken, string payeeToken, decimal amount) =>
        ledger.Reverse(payerToken, payeeToken, amount);
}
