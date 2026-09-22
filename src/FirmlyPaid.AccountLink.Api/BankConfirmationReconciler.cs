using FirmlyPaid.Data.Core;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.AccountLink.Api;

/// <summary>
/// Asks the bank about confirmations we are still waiting on, and applies whatever it says.
/// </summary>
/// <remarks>
/// The real bank calls <c>POST /bank-callbacks/confirmations</c> when the customer taps
/// approve. A callback can be lost, so a poller is needed either way; while the bank is
/// simulated it is also the only thing that moves an account from Pending to Confirmed.
/// Both paths end in <see cref="AccountLinkService.ApplyConfirmationAsync"/>, so there is
/// one place where an account becomes payable.
/// </remarks>
public sealed class BankConfirmationReconciler(
    IServiceScopeFactory scopeFactory,
    IBankGateway bank,
    ILogger<BankConfirmationReconciler> logger)
{
    /// <summary>Runs one pass and returns how many accounts changed state.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FirmlyPaidCoreDbContext>();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountLinkService>();

        var waiting = await database.LinkedAccounts
            .AsNoTracking()
            .Where(a => a.Status == LinkedAccountStatus.Pending && a.ConfirmationReference != null)
            .Select(a => a.ConfirmationReference!)
            .ToListAsync(ct);

        var changed = 0;

        foreach (var reference in waiting)
        {
            AccountConfirmationOutcome outcome;

            try
            {
                outcome = await bank.GetConfirmationStatusAsync(reference, ct);
            }
            catch (KeyNotFoundException)
            {
                // The fake bank keeps confirmations in memory, so a restart loses them.
                // The account stays Pending and the customer can link it again.
                logger.LogInformation("The bank no longer recognises a confirmation we are waiting on");
                continue;
            }

            if (outcome == AccountConfirmationOutcome.Pending)
            {
                continue;
            }

            if (await accounts.ApplyConfirmationAsync(reference, outcome, ct))
            {
                changed++;
            }
        }

        return changed;
    }
}

/// <summary>Runs the reconciler on a timer for as long as the service is up.</summary>
public sealed class BankConfirmationPoller(
    BankConfirmationReconciler reconciler,
    ILogger<BankConfirmationPoller> logger) : BackgroundService
{
    // Often enough that a demo does not feel stuck, seldom enough to be invisible.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await reconciler.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad pass must not take the poller down for the rest of the day.
                logger.LogError(ex, "A bank confirmation pass failed. Trying again on the next tick.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
