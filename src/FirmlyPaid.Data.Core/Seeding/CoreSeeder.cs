using FirmlyPaid.Data.Core.Entities;
using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.Data.Core.Seeding;

/// <summary>
/// Writes the demo cast into FirmlyPaidCore. Safe to run more than once: it checks for
/// the fixed merchant ids and does nothing if they are already there.
/// </summary>
public sealed class CoreSeeder(
    FirmlyPaidCoreDbContext database,
    IIdentityHasher identityHasher,
    IPinHasher pinHasher,
    IClock clock)
{
    public sealed record SeedResult(
        bool AlreadySeeded,
        int Merchants,
        int Stores,
        int Terminals,
        int Customers,
        int LinkedAccounts);

    public async Task<SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var firstMerchantId = SeedCatalogue.Merchants[0].MerchantId;
        if (await database.Merchants.AnyAsync(m => m.MerchantId == firstMerchantId, ct))
        {
            return new SeedResult(AlreadySeeded: true, 0, 0, 0, 0, 0);
        }

        var now = clock.UtcNow;

        // Hashed once and reused: Argon2id is slow on purpose, and every seeded customer
        // shares the same demo PIN anyway.
        var demoPinHash = pinHasher.Hash(SeedCatalogue.DemoPin);

        var stores = 0;
        var terminals = 0;

        foreach (var seedMerchant in SeedCatalogue.Merchants)
        {
            var merchant = new Merchant
            {
                MerchantId = seedMerchant.MerchantId,
                TradingName = seedMerchant.TradingName,
                RegistrationNumber = seedMerchant.RegistrationNumber,
                FeePercent = seedMerchant.FeePercent,
                Tier = seedMerchant.Tier,
                AnnualLicenceFee = seedMerchant.AnnualLicenceFee,
                Status = MerchantStatus.Active,
                CreatedAt = now,
            };
            database.Merchants.Add(merchant);

            foreach (var seedStore in seedMerchant.Stores)
            {
                database.Stores.Add(new Store
                {
                    StoreId = seedStore.StoreId,
                    MerchantId = merchant.MerchantId,
                    Name = seedStore.Name,
                    Address = seedStore.Address,
                    Area = seedStore.Area,
                });
                stores++;

                foreach (var seedTerminal in seedStore.Terminals)
                {
                    database.Terminals.Add(new Terminal
                    {
                        TerminalId = seedTerminal.TerminalId,
                        StoreId = seedStore.StoreId,
                        SerialNumber = seedTerminal.SerialNumber,
                        CertificateThumbprint = seedTerminal.CertificateThumbprint,
                        Type = seedTerminal.Type,
                        MonthlyRental = seedTerminal.MonthlyRental,
                        Status = TerminalStatus.Active,
                        CreatedAt = now,
                    });
                    terminals++;
                }
            }
        }

        var enrolmentStoreId = SeedCatalogue.Merchants[0].Stores[0].StoreId;
        var customers = 0;
        var linkedAccounts = 0;

        foreach (var person in SeedCatalogue.People)
        {
            var idNumber = person.IdNumber;

            var customer = new Customer
            {
                FullName = person.FullName,
                // The ID number itself goes no further than this line (rule 10.4).
                IdNumberHash = identityHasher.HashIdNumber(idNumber),
                IdDigits7to10Bucket = person.Bucket,
                CellphoneNumber = person.CellphoneNumber,
                PinHash = demoPinHash,
                Status = person.Status,
                Tier = CustomerTier.Pilot,
                FrozenAt = person.Status == CustomerStatus.Frozen ? now : null,
                CreatedAt = now,
            };
            database.Customers.Add(customer);
            customers++;

            database.Consents.Add(new Consent
            {
                CustomerId = customer.CustomerId,
                ConsentTextVersion = SeedCatalogue.DemoConsentTextVersion,
                AgentId = SeedCatalogue.DemoAgentId,
                AcceptedAt = now,
            });

            database.Enrolments.Add(new Enrolment
            {
                CustomerId = customer.CustomerId,
                AgentId = SeedCatalogue.DemoAgentId,
                StoreId = enrolmentStoreId,
                FullName = person.FullName,
                IdNumberHash = customer.IdNumberHash,
                IdDigits7to10Bucket = customer.IdDigits7to10Bucket,
                CellphoneNumber = person.CellphoneNumber,
                TemplateOwnerId = customer.TemplateOwnerId,
                ConsentTextVersion = SeedCatalogue.DemoConsentTextVersion,
                ConsentAcceptedAt = now,
                HomeAffairsResult = HomeAffairsOutcome.Match,
                HomeAffairsReference = $"HA-SEED-{customers:D3}",
                Status = EnrolmentStatus.Completed,
                FingersCaptured = 2,
                CreatedAt = now,
                CompletedAt = now,
            });

            LinkedAccount? firstAccount = null;

            foreach (var seedAccount in person.Accounts)
            {
                var account = new LinkedAccount
                {
                    CustomerId = customer.CustomerId,
                    BankCode = seedAccount.BankCode,
                    AccountNickname = seedAccount.Nickname,
                    AccountLast4 = Masking.Last4(seedAccount.AccountNumber),
                    // The encrypted bank token is filled in from step 5, once the fake bank
                    // exists to issue one. Until then there is nothing honest to store.
                    AccountTokenCiphertext = null,
                    AccountTokenKeyVersion = null,
                    Status = LinkedAccountStatus.Confirmed,
                    ConfirmationReference = $"SEED-CONF-{customers:D3}-{seedAccount.BankCode}",
                    CreatedAt = now,
                    ConfirmedAt = now,
                };
                database.LinkedAccounts.Add(account);
                linkedAccounts++;

                firstAccount ??= account;
            }

            if (person.SetDefaultAccount && firstAccount is not null)
            {
                customer.DefaultLinkedAccountId = firstAccount.LinkedAccountId;
            }
        }

        await database.AppendAuditAsync(
            actor: "system",
            action: "SeedApplied",
            entityType: "Database",
            entityId: "FirmlyPaidCore",
            details: $"Seeded {SeedCatalogue.Merchants.Count} merchants, {terminals} terminals, {customers} customers.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);

        return new SeedResult(false, SeedCatalogue.Merchants.Count, stores, terminals, customers, linkedAccounts);
    }

    /// <summary>
    /// Deletes every seeded and non-seeded row. Used by "dbtool reset" on a developer
    /// laptop only. Order follows the foreign keys.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        await database.Disputes.ExecuteDeleteAsync(ct);
        await database.Refunds.ExecuteDeleteAsync(ct);
        await database.RiskEvents.ExecuteDeleteAsync(ct);
        await database.Payments.ExecuteDeleteAsync(ct);
        await database.Settlements.ExecuteDeleteAsync(ct);

        // Clear the default before the accounts go, or the customer points at nothing.
        await database.Customers.ExecuteUpdateAsync(
            update => update.SetProperty(c => c.DefaultLinkedAccountId, (Guid?)null), ct);

        await database.LinkedAccounts.ExecuteDeleteAsync(ct);
        await database.Consents.ExecuteDeleteAsync(ct);
        await database.Enrolments.ExecuteDeleteAsync(ct);
        await database.Customers.ExecuteDeleteAsync(ct);
        await database.Terminals.ExecuteDeleteAsync(ct);
        await database.Stores.ExecuteDeleteAsync(ct);
        await database.Merchants.ExecuteDeleteAsync(ct);
        await database.AuditLog.ExecuteDeleteAsync(ct);
    }
}
