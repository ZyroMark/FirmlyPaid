using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Simulators.Bank;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// The fake bank has to behave like the real one on the paths that matter: money really
/// moves, a retry does not charge twice (rule 10.11), and an account is only usable once
/// the customer has confirmed it in their own app (FR-04).
/// </summary>
public class BankGatewayTests
{
    private readonly FakeBankLedger _ledger = new();
    private readonly SimulatorControlState _control = new();
    private readonly TestClock _clock = new();
    private readonly SimulatedBankGateway _bank;

    public BankGatewayTests()
    {
        _bank = new SimulatedBankGateway(_ledger, _control, _clock, NullLogger<SimulatedBankGateway>.Instance);
    }

    [Theory]
    [InlineData(BankCodes.Absa)]
    [InlineData(BankCodes.Fnb)]
    [InlineData(BankCodes.Discovery)]
    [InlineData(BankCodes.TymeBank)]
    public async Task AllFourPilotBanksCanConfirmAnAccount(string bankCode)
    {
        var result = await RequestConfirmationAsync(bankCode);

        result.Outcome.Should().Be(AccountConfirmationOutcome.Pending);
        result.AccountToken.Should().NotBeNullOrWhiteSpace();
        result.AccountLast4.Should().Be("4567");
    }

    [Fact]
    public async Task AnUnknownBankIsRefused()
    {
        var act = async () => await RequestConfirmationAsync("MONOPOLY");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnAccountStaysPendingUntilTheCustomerOpensTheirApp()
    {
        // Part 7: auto-approve after five seconds. Until then it is pending, and an
        // unconfirmed account must never appear at a till.
        var requested = await RequestConfirmationAsync(BankCodes.Absa);

        (await _bank.GetConfirmationStatusAsync(requested.ConfirmationReference))
            .Should().Be(AccountConfirmationOutcome.Pending);

        _clock.Advance(TimeSpan.FromSeconds(5));

        (await _bank.GetConfirmationStatusAsync(requested.ConfirmationReference))
            .Should().Be(AccountConfirmationOutcome.Confirmed);
    }

    [Fact]
    public async Task TheCustomerCanRejectTheConfirmation()
    {
        _control.AccountConfirmation = AccountConfirmationMode.Reject;
        var requested = await RequestConfirmationAsync(BankCodes.Fnb);

        _clock.Advance(TimeSpan.FromSeconds(5));

        (await _bank.GetConfirmationStatusAsync(requested.ConfirmationReference))
            .Should().Be(AccountConfirmationOutcome.Rejected);
    }

    [Fact]
    public async Task AnIgnoredConfirmationNeverResolves()
    {
        _control.AccountConfirmation = AccountConfirmationMode.NeverAnswer;
        var requested = await RequestConfirmationAsync(BankCodes.TymeBank);

        _clock.Advance(TimeSpan.FromHours(1));

        (await _bank.GetConfirmationStatusAsync(requested.ConfirmationReference))
            .Should().Be(AccountConfirmationOutcome.Pending);
    }

    [Fact]
    public async Task AnApprovedPaymentActuallyMovesTheMoney()
    {
        var (payer, payee) = await OpenTwoAccountsAsync();

        var result = await PayAsync(payer, payee, 250.00m, "pay-1");

        result.IsApproved.Should().BeTrue();
        result.BankReference.Should().NotBeNullOrWhiteSpace();

        _ledger.BalanceOf(payer).Should().Be(FakeBankLedger.OpeningBalance - 250.00m);
        _ledger.BalanceOf(payee).Should().Be(250.00m);
    }

    [Fact]
    public async Task APayerWithoutEnoughMoneyIsDeclinedAndNothingMoves()
    {
        var (payer, payee) = await OpenTwoAccountsAsync();
        _ledger.SetBalance(payer, 100.00m);

        var result = await PayAsync(payer, payee, 250.00m, "pay-2");

        result.Outcome.Should().Be(BankPaymentOutcome.InsufficientFunds);
        _ledger.BalanceOf(payer).Should().Be(100.00m);
        _ledger.BalanceOf(payee).Should().Be(0m);
    }

    [Fact]
    public async Task TheSameIdempotencyKeyNeverChargesTwice()
    {
        // Rule 10.11. A terminal that loses the answer and retries must not double charge.
        var (payer, payee) = await OpenTwoAccountsAsync();

        var first = await PayAsync(payer, payee, 250.00m, "same-key");
        var retry = await PayAsync(payer, payee, 250.00m, "same-key");

        first.IsApproved.Should().BeTrue();
        retry.BankReference.Should().Be(first.BankReference, "the retry gets the original answer back");
        _ledger.BalanceOf(payer).Should().Be(FakeBankLedger.OpeningBalance - 250.00m);
    }

    [Fact]
    public async Task APaymentWithoutAnIdempotencyKeyIsRefused()
    {
        var (payer, payee) = await OpenTwoAccountsAsync();

        var act = async () => await PayAsync(payer, payee, 250.00m, "   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheBankCanBeMadeToDecline()
    {
        var (payer, payee) = await OpenTwoAccountsAsync();
        _control.BankPayment = BankPaymentMode.AlwaysDecline;

        var result = await PayAsync(payer, payee, 250.00m, "pay-3");

        result.Outcome.Should().Be(BankPaymentOutcome.Declined);
        result.DeclineReason.Should().NotBeNullOrWhiteSpace();
        _ledger.BalanceOf(payer).Should().Be(FakeBankLedger.OpeningBalance);
    }

    [Fact]
    public async Task ATimingOutBankNeverAnswers()
    {
        // FR-10: the payment service gives up after its own timeout and marks the payment
        // Failed. The bank simply does not reply.
        var (payer, payee) = await OpenTwoAccountsAsync();
        _control.BankPayment = BankPaymentMode.Timeout;

        using var giveUpAfter = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = async () => await _bank.PayAsync(
            new BankPaymentRequest(BankCodes.Absa, payer, payee, 250.00m, "TILL-1", "pay-4"),
            giveUpAfter.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _ledger.BalanceOf(payer).Should().Be(FakeBankLedger.OpeningBalance);
    }

    [Fact]
    public async Task ARefundSucceedsAndPutsTheMoneyBack()
    {
        var (payer, payee) = await OpenTwoAccountsAsync();
        var payment = await PayAsync(payer, payee, 250.00m, "pay-5");

        var refund = await _bank.RefundAsync(new BankRefundRequest(
            BankCodes.Absa, payment.BankReference!, 250.00m, "Customer returned the item", "refund-1"));

        refund.Succeeded.Should().BeTrue();

        _bank.ApplyRefundToLedger(payer, payee, 250.00m);
        _ledger.BalanceOf(payer).Should().Be(FakeBankLedger.OpeningBalance);
        _ledger.BalanceOf(payee).Should().Be(0m);
    }

    [Fact]
    public async Task ARefundWithoutTheOriginalReferenceFails()
    {
        var refund = await _bank.RefundAsync(new BankRefundRequest(
            BankCodes.Absa, "", 250.00m, "No original", "refund-2"));

        refund.Succeeded.Should().BeFalse();
        refund.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NoBankResponseCarriesABalance()
    {
        // Rule 10.7: a balance never appears in an API response, only in the fake ledger.
        var (payer, payee) = await OpenTwoAccountsAsync();

        var approved = await PayAsync(payer, payee, 250.00m, "pay-6");
        var declined = await PayAsync(payer, payee, 100000.00m, "pay-7");

        typeof(BankPaymentResult).GetProperties().Select(p => p.Name)
            .Should().NotContain(name => name.Contains("Balance", StringComparison.OrdinalIgnoreCase));

        declined.DeclineReason.Should().NotContain("5000");
        approved.IsApproved.Should().BeTrue();
    }

    private Task<AccountConfirmationRequestResult> RequestConfirmationAsync(string bankCode) =>
        _bank.RequestAccountConfirmationAsync(
            new AccountConfirmationRequest(bankCode, "4051234567", "Thandiwe Mokoena", "0821000001"));

    private async Task<(string Payer, string Payee)> OpenTwoAccountsAsync()
    {
        var confirmation = await RequestConfirmationAsync(BankCodes.Absa);
        var merchantAccount = _ledger.OpenMerchantAccount(BankCodes.Absa);

        return (confirmation.AccountToken, merchantAccount);
    }

    private Task<BankPaymentResult> PayAsync(string payer, string payee, decimal amount, string key) =>
        _bank.PayAsync(new BankPaymentRequest(BankCodes.Absa, payer, payee, amount, "TILL-1", key));
}
