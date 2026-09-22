using FirmlyPaid.Data.Core;
using FirmlyPaid.Data.Core.Entities;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.AccountLink.Api;

/// <summary>
/// Linking a bank account to a customer, and keeping track of which one they pay from by
/// default. The account number passes through once and is never stored: the bank returns a
/// token, and even that is encrypted at rest (rule 10.7 and 10.8).
/// </summary>
public sealed class AccountLinkService(
    FirmlyPaidCoreDbContext database,
    IBankGateway bank,
    IKeyVault keyVault,
    ISmsSender sms,
    IOptions<FirmlyPaidOptions> options,
    IClock clock,
    ILogger<AccountLinkService> logger)
{
    private readonly EnrolmentOptions _options = options.Value.Enrolment;

    /// <summary>
    /// Asks the bank to have the customer confirm the account in their own banking app.
    /// The account is Pending until they do, and a Pending account can never be paid from
    /// (FR-04).
    /// </summary>
    public async Task<LinkAccountResponse> LinkAsync(
        Guid customerId,
        LinkAccountRequest request,
        CancellationToken ct = default)
    {
        var customer = await RequireActiveCustomerAsync(customerId, ct);

        if (!BankCodes.IsKnown(request.BankCode))
        {
            throw FirmlyPaidException.Validation("That bank is not supported yet.");
        }

        if (!IsPlausibleAccountNumber(request.AccountNumber))
        {
            throw FirmlyPaidException.Validation("That account number does not look right.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountNickname))
        {
            throw FirmlyPaidException.Validation("Please give the account a name, for example Salary.");
        }

        // A Pending account still holds a slot. Otherwise someone could fill the list with
        // requests they never confirm and lock themselves out of adding a real one.
        var inUse = await database.LinkedAccounts
            .CountAsync(a => a.CustomerId == customerId && a.Status != LinkedAccountStatus.Removed, ct);

        if (inUse >= _options.MaxLinkedAccounts)
        {
            throw new FirmlyPaidException(
                ErrorCodes.TooManyAccounts,
                $"You can link up to {_options.MaxLinkedAccounts} bank accounts. Please remove one first.");
        }

        var bankCode = request.BankCode.ToUpperInvariant();

        var confirmation = await bank.RequestAccountConfirmationAsync(
            new AccountConfirmationRequest(
                bankCode,
                request.AccountNumber,
                customer.FullName,
                customer.CellphoneNumber),
            ct);

        // The token is the bank's handle for the account. It is as good as the account
        // number to anyone who steals it, so it never sits in the database in the clear.
        var protectedToken = await keyVault.ProtectAsync(
            System.Text.Encoding.UTF8.GetBytes(confirmation.AccountToken), ct);

        var now = clock.UtcNow;

        var account = new LinkedAccount
        {
            CustomerId = customerId,
            BankCode = bankCode,
            AccountNickname = request.AccountNickname.Trim(),
            AccountLast4 = confirmation.AccountLast4,
            AccountTokenCiphertext = protectedToken.Ciphertext,
            AccountTokenKeyVersion = protectedToken.KeyVersion,
            ConfirmationReference = confirmation.ConfirmationReference,
            Status = LinkedAccountStatus.Pending,
            CreatedAt = now,
        };

        database.LinkedAccounts.Add(account);

        await database.AppendAuditAsync(
            actor: $"customer:{customerId}",
            action: "AccountLinkRequested",
            entityType: "LinkedAccount",
            entityId: account.LinkedAccountId.ToString(),
            details: $"Asked {BankCodes.DisplayName(bankCode)} to confirm an account ending {account.AccountLast4}.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);

        await sms.SendAsync(
            customer.CellphoneNumber,
            $"FirmlyPaid: open your {BankCodes.DisplayName(bankCode)} app to confirm the account ending {account.AccountLast4}.",
            ct);

        logger.LogInformation(
            "Account {LinkedAccountId} linked to {BankCode} and waiting for the customer to confirm",
            account.LinkedAccountId,
            bankCode);

        return new LinkAccountResponse(
            account.LinkedAccountId,
            account.Status,
            account.AccountLast4,
            confirmation.ConfirmationReference);
    }

    /// <summary>
    /// Applies what the bank said about a confirmation. Both the signed callback and the
    /// poller that stands in for it while the bank is simulated come through here, so
    /// there is one place where an account becomes payable.
    /// </summary>
    public async Task<bool> ApplyConfirmationAsync(
        string confirmationReference,
        AccountConfirmationOutcome outcome,
        CancellationToken ct = default)
    {
        var account = await database.LinkedAccounts
            .FirstOrDefaultAsync(a => a.ConfirmationReference == confirmationReference, ct);

        if (account is null)
        {
            // Not an error worth shouting about: a bank may retry a callback for an
            // account we have since removed. Answering 200 stops it retrying forever.
            logger.LogInformation("A bank confirmation arrived for a reference we no longer hold");
            return false;
        }

        if (account.Status != LinkedAccountStatus.Pending)
        {
            logger.LogInformation("A bank confirmation arrived for an account that is already settled");
            return false;
        }

        var now = clock.UtcNow;

        switch (outcome)
        {
            case AccountConfirmationOutcome.Confirmed:
                account.Status = LinkedAccountStatus.Confirmed;
                account.ConfirmedAt = now;
                break;

            case AccountConfirmationOutcome.Rejected:
                // The customer said this is not their account. It never becomes payable.
                account.Status = LinkedAccountStatus.Removed;
                account.RemovedAt = now;
                break;

            default:
                return false;
        }

        await database.AppendAuditAsync(
            actor: $"bank:{account.BankCode}",
            action: outcome == AccountConfirmationOutcome.Confirmed ? "AccountConfirmed" : "AccountRejected",
            entityType: "LinkedAccount",
            entityId: account.LinkedAccountId.ToString(),
            details: $"{BankCodes.DisplayName(account.BankCode)} answered {outcome} for the account ending {account.AccountLast4}.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);

        logger.LogInformation("Account {LinkedAccountId} is now {Status}", account.LinkedAccountId, account.Status);

        return true;
    }

    /// <summary>
    /// Every account the customer can see, including the ones still waiting to be
    /// confirmed, so the portal can tell them what is outstanding. Never a balance
    /// (rule 10.7).
    /// </summary>
    public Task<List<LinkedAccountSummary>> ListAsync(Guid customerId, CancellationToken ct = default) =>
        SummariseAsync(customerId, payableOnly: false, ct);

    /// <summary>
    /// Only the accounts a payment may actually use. This is what the bank picker at a
    /// till is built from, which is why a Pending account can never appear there (FR-04).
    /// </summary>
    public Task<List<LinkedAccountSummary>> ListPayableAsync(Guid customerId, CancellationToken ct = default) =>
        SummariseAsync(customerId, payableOnly: true, ct);

    /// <summary>Sets the account the bank picker skips straight to (FR-05).</summary>
    public async Task SetDefaultAsync(Guid customerId, Guid linkedAccountId, CancellationToken ct = default)
    {
        var customer = await RequireActiveCustomerAsync(customerId, ct);

        var account = await database.LinkedAccounts
            .FirstOrDefaultAsync(a => a.LinkedAccountId == linkedAccountId && a.CustomerId == customerId, ct)
            ?? throw FirmlyPaidException.NotFound("That account");

        // A default the customer cannot actually pay from would strand them at the till.
        if (account.Status != LinkedAccountStatus.Confirmed)
        {
            throw FirmlyPaidException.AccountNotConfirmed();
        }

        customer.DefaultLinkedAccountId = account.LinkedAccountId;

        await database.AppendAuditAsync(
            actor: $"customer:{customerId}",
            action: "DefaultAccountSet",
            entityType: "LinkedAccount",
            entityId: account.LinkedAccountId.ToString(),
            details: $"Default set to the {BankCodes.DisplayName(account.BankCode)} account ending {account.AccountLast4}.",
            createdAtUtc: clock.UtcNow,
            ct: ct);

        await database.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes an account. If it was the default, the default is cleared in the same
    /// change, so the customer is never pointed at an account that is no longer there
    /// (FR-05).
    /// </summary>
    public async Task RemoveAsync(Guid customerId, Guid linkedAccountId, CancellationToken ct = default)
    {
        var customer = await RequireActiveCustomerAsync(customerId, ct);

        var account = await database.LinkedAccounts
            .FirstOrDefaultAsync(a => a.LinkedAccountId == linkedAccountId
                                   && a.CustomerId == customerId
                                   && a.Status != LinkedAccountStatus.Removed, ct)
            ?? throw FirmlyPaidException.NotFound("That account");

        var now = clock.UtcNow;

        account.Status = LinkedAccountStatus.Removed;
        account.RemovedAt = now;

        // The token is of no further use to us, and keeping it would only be a liability.
        account.AccountTokenCiphertext = null;
        account.AccountTokenKeyVersion = null;

        var wasDefault = customer.DefaultLinkedAccountId == account.LinkedAccountId;
        if (wasDefault)
        {
            customer.DefaultLinkedAccountId = null;
        }

        await database.AppendAuditAsync(
            actor: $"customer:{customerId}",
            action: "AccountRemoved",
            entityType: "LinkedAccount",
            entityId: account.LinkedAccountId.ToString(),
            details: wasDefault
                ? $"Removed the default {BankCodes.DisplayName(account.BankCode)} account ending {account.AccountLast4}. The default is now unset."
                : $"Removed the {BankCodes.DisplayName(account.BankCode)} account ending {account.AccountLast4}.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);
    }

    private async Task<List<LinkedAccountSummary>> SummariseAsync(Guid customerId, bool payableOnly, CancellationToken ct)
    {
        var customer = await database.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
            ?? throw FirmlyPaidException.NotFound("That customer");

        var query = database.LinkedAccounts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId);

        query = payableOnly
            ? query.Where(a => a.Status == LinkedAccountStatus.Confirmed)
            : query.Where(a => a.Status != LinkedAccountStatus.Removed);

        var accounts = await query.OrderBy(a => a.CreatedAt).ToListAsync(ct);

        // Nickname, bank and last 4 only. No balance appears here or anywhere else.
        return
        [
            .. accounts.Select(a => new LinkedAccountSummary(
                a.LinkedAccountId,
                a.BankCode,
                BankCodes.DisplayName(a.BankCode),
                a.AccountNickname,
                a.AccountLast4,
                customer.DefaultLinkedAccountId == a.LinkedAccountId,
                a.Status))
        ];
    }

    private async Task<Customer> RequireActiveCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await database.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
            ?? throw FirmlyPaidException.NotFound("That customer");

        return customer.Status switch
        {
            CustomerStatus.Active => customer,
            CustomerStatus.Frozen => throw FirmlyPaidException.CustomerFrozen(),
            _ => throw FirmlyPaidException.NotFound("That customer"),
        };
    }

    /// <summary>South African account numbers run to about eleven digits.</summary>
    private static bool IsPlausibleAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return false;
        }

        foreach (var c in accountNumber)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return accountNumber.Length is >= 6 and <= 12;
    }
}
