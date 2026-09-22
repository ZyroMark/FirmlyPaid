using System.Collections.Concurrent;
using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Simulators.Bank;

/// <summary>
/// The books behind the four fake banks. Real balances that go down when a payment is made
/// and up on a refund, so a test can prove money actually moved rather than trusting a
/// status code (part 7).
/// </summary>
/// <remarks>
/// A balance never leaves this class. Rule 10.7 forbids a balance appearing on any screen
/// or in any API response, so the gateway reads it to make a decision and reports only
/// approved or insufficient funds. Tests read it directly, which is the one exception.
/// </remarks>
public sealed class FakeBankLedger
{
    /// <summary>What every newly confirmed account starts with.</summary>
    public const decimal OpeningBalance = 5000.00m;

    private readonly ConcurrentDictionary<string, Account> _accounts = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    private sealed class Account
    {
        public required string BankCode { get; init; }
        public required string AccountLast4 { get; init; }
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Opens an account and hands back the opaque token FirmlyPaid stores in place of the
    /// account number. The token is random, so it reveals nothing about the account.
    /// </summary>
    public string OpenAccount(string bankCode, string accountLast4, decimal openingBalance = OpeningBalance)
    {
        var token = $"{bankCode.ToLowerInvariant()}-tok-{Guid.NewGuid():N}";

        _accounts[token] = new Account
        {
            BankCode = bankCode,
            AccountLast4 = accountLast4,
            Balance = openingBalance,
        };

        return token;
    }

    /// <summary>The merchant's settlement account, which payments are paid into.</summary>
    public string OpenMerchantAccount(string bankCode) => OpenAccount(bankCode, "0000", openingBalance: 0m);

    public bool Knows(string accountToken) => _accounts.ContainsKey(accountToken);

    /// <summary>For tests and the admin page only. Never returned by any API (rule 10.7).</summary>
    public decimal BalanceOf(string accountToken) =>
        _accounts.TryGetValue(accountToken, out var account)
            ? account.Balance
            : throw new KeyNotFoundException("That account token is not known to the fake bank.");

    public void SetBalance(string accountToken, decimal balance)
    {
        lock (_gate)
        {
            _accounts[accountToken].Balance = balance;
        }
    }

    /// <summary>
    /// Moves money between two accounts, or says why it could not. Both sides move under
    /// one lock, so a concurrent payment can never see half a transfer.
    /// </summary>
    public BankPaymentOutcome Transfer(string payerToken, string payeeToken, decimal amount)
    {
        if (amount <= 0)
        {
            return BankPaymentOutcome.Declined;
        }

        lock (_gate)
        {
            if (!_accounts.TryGetValue(payerToken, out var payer) ||
                !_accounts.TryGetValue(payeeToken, out var payee))
            {
                return BankPaymentOutcome.Declined;
            }

            if (payer.Balance < amount)
            {
                return BankPaymentOutcome.InsufficientFunds;
            }

            payer.Balance -= amount;
            payee.Balance += amount;

            return BankPaymentOutcome.Approved;
        }
    }

    /// <summary>Sends money back the other way. A refund is not refused for funds.</summary>
    public void Reverse(string payerToken, string payeeToken, decimal amount)
    {
        lock (_gate)
        {
            if (_accounts.TryGetValue(payerToken, out var payer) &&
                _accounts.TryGetValue(payeeToken, out var payee))
            {
                payee.Balance -= amount;
                payer.Balance += amount;
            }
        }
    }

    /// <summary>Empties the books between demos.</summary>
    public void Clear() => _accounts.Clear();
}
