using FirmlyPaid.Data.Core.Entities;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// Step 2 checkpoint: the migrations produce the tables from part 5, with the constraints
/// the security rules depend on actually enforced by the database.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class CoreSchemaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Migration_CreatesEveryTableFromPart5()
    {
        await using var database = await fixture.CreateCoreAsync();

        var tables = await database.Database
            .SqlQuery<string>($"SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync();

        tables.Should().Contain(
        [
            "Customers", "Consents", "LinkedAccounts", "Merchants", "Stores", "Terminals",
            "Payments", "Refunds", "Disputes", "RiskEvents", "Settlements", "AuditLog", "Enrolments",
            "SmsMessages",
        ]);
    }

    [Fact]
    public async Task Payments_RefuseASecondRowWithTheSameIdempotencyKey()
    {
        // Rule 10.11: a retry must never charge twice, and the database is the last line.
        await using var database = await fixture.CreateCoreAsync();

        var terminal = await AddTerminalAsync(database);

        database.Payments.Add(NewPayment("retry-me", terminal));
        await database.SaveChangesAsync();

        database.Payments.Add(NewPayment("retry-me", terminal));

        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.And.InnerException.Should().BeOfType<SqlException>();
    }

    [Fact]
    public async Task Customers_RefuseTwoProfilesForTheSamePerson()
    {
        await using var database = await fixture.CreateCoreAsync();

        database.Customers.Add(NewCustomer("same-person-hash"));
        await database.SaveChangesAsync();

        database.Customers.Add(NewCustomer("same-person-hash"));

        var act = async () => await database.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Terminals_RefuseADuplicateCertificateThumbprint()
    {
        // Rule 10.9: one certificate, one terminal. Two rows would make trust ambiguous.
        await using var database = await fixture.CreateCoreAsync();

        var merchant = new Merchant { TradingName = "Test Merchant", RegistrationNumber = "2026/000001/07", CreatedAt = DateTime.UtcNow };
        var store = new Store { MerchantId = merchant.MerchantId, Name = "Test Store", Address = "1 Test Road", Area = "Test" };
        database.Merchants.Add(merchant);
        database.Stores.Add(store);

        database.Terminals.Add(new Terminal
        {
            StoreId = store.StoreId,
            SerialNumber = "FP-9001",
            CertificateThumbprint = "duplicate-thumbprint",
            CreatedAt = DateTime.UtcNow,
        });
        await database.SaveChangesAsync();

        database.Terminals.Add(new Terminal
        {
            StoreId = store.StoreId,
            SerialNumber = "FP-9002",
            CertificateThumbprint = "duplicate-thumbprint",
            CreatedAt = DateTime.UtcNow,
        });

        var act = async () => await database.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Money_IsStoredAsDecimal18By2()
    {
        await using var database = await fixture.CreateCoreAsync();

        var columns = await database.Database
            .SqlQuery<MoneyColumn>($"""
                SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName,
                       CAST(NUMERIC_PRECISION AS int) AS Precision, CAST(NUMERIC_SCALE AS int) AS Scale
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME IN ('Payments', 'Refunds', 'Settlements')
                  AND DATA_TYPE = 'decimal'
                """)
            .ToListAsync();

        columns.Should().NotBeEmpty();
        columns.Should().OnlyContain(c => c.Precision == 18 && c.Scale == 2);
    }

    [Fact]
    public async Task Times_ComeBackAsUtc()
    {
        // SQL Server does not store the kind, so a naive read turns UTC into local time.
        await using var database = await fixture.CreateCoreAsync();

        var terminal = await AddTerminalAsync(database);

        var written = new DateTime(2026, 9, 21, 14, 30, 0, DateTimeKind.Utc);
        var payment = NewPayment("utc-check", terminal);
        payment.CreatedAt = written;
        database.Payments.Add(payment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var read = await database.Payments.SingleAsync(p => p.IdempotencyKey == "utc-check");

        read.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        read.CreatedAt.Should().Be(written);
    }

    /// <summary>A merchant, a store and a terminal, so a payment has something to hang off.</summary>
    private static async Task<Terminal> AddTerminalAsync(FirmlyPaid.Data.Core.FirmlyPaidCoreDbContext database)
    {
        var merchant = new Merchant
        {
            TradingName = "Test Merchant",
            RegistrationNumber = $"2026/{Random.Shared.Next(100000, 999999)}/07",
            CreatedAt = DateTime.UtcNow,
        };
        var store = new Store
        {
            MerchantId = merchant.MerchantId,
            Name = "Test Store",
            Address = "1 Test Road",
            Area = "Test",
        };
        var terminal = new Terminal
        {
            StoreId = store.StoreId,
            SerialNumber = $"FP-{Random.Shared.Next(100000, 999999)}",
            CertificateThumbprint = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
        };

        database.Merchants.Add(merchant);
        database.Stores.Add(store);
        database.Terminals.Add(terminal);
        await database.SaveChangesAsync();

        return terminal;
    }

    private static Payment NewPayment(string idempotencyKey, Terminal terminal) => new()
    {
        TerminalId = terminal.TerminalId,
        MerchantId = Guid.NewGuid(),
        StoreId = terminal.StoreId,
        Amount = 250.00m,
        FeeAmount = 3.00m,
        MerchantReference = "TILL-1",
        IdempotencyKey = idempotencyKey,
        CreatedAt = DateTime.UtcNow,
    };

    private static Customer NewCustomer(string idNumberHash) => new()
    {
        FullName = "Test Person",
        IdNumberHash = idNumberHash,
        IdDigits7to10Bucket = 1234,
        CellphoneNumber = "0820000000",
        CreatedAt = DateTime.UtcNow,
    };

    private sealed record MoneyColumn(string TableName, string ColumnName, int Precision, int Scale);
}
