using FirmlyPaid.Data.Core.Seeding;
using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// Part 7: the seed gives every flow something to demonstrate within a few minutes,
/// and it can be run twice without making a mess.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class SeedTests(SqlServerFixture fixture)
{
    private static readonly IIdentityHasher Hasher =
        new HmacIdentityHasher("a-test-pepper-that-is-long-enough-to-pass");

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Seed_CreatesThreeMerchantsFiveTerminalsAndTenCustomers()
    {
        await using var database = await fixture.CreateCoreAsync();

        var result = await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        result.AlreadySeeded.Should().BeFalse();
        result.Merchants.Should().Be(3);
        result.Terminals.Should().Be(5);
        result.Customers.Should().Be(10);

        (await database.Merchants.CountAsync()).Should().Be(3);
        (await database.Terminals.CountAsync()).Should().Be(5);
        (await database.Customers.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task Seed_GivesSomeCustomersOneBankAndOthersThree()
    {
        await using var database = await fixture.CreateCoreAsync();
        await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        var accountCounts = await database.LinkedAccounts
            .GroupBy(a => a.CustomerId)
            .Select(g => g.Count())
            .ToListAsync();

        accountCounts.Should().Contain(1, "some customers must skip the bank picker");
        accountCounts.Should().Contain(3, "others must see a picker with three cards (FR-08)");
        accountCounts.Should().OnlyContain(count => count <= 5, "a customer may link at most five (FR-04)");
    }

    [Fact]
    public async Task Seed_LeavesTwoCustomersSharingABucket()
    {
        // Matching must still pick the right person when a bucket holds more than one.
        await using var database = await fixture.CreateCoreAsync();
        await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        var crowdedBuckets = await database.Customers
            .GroupBy(c => c.IdDigits7to10Bucket)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        crowdedBuckets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Seed_StoresNoIdNumberInTheClear()
    {
        await using var database = await fixture.CreateCoreAsync();
        await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        var hashes = await database.Customers.Select(c => c.IdNumberHash).ToListAsync();

        hashes.Should().OnlyHaveUniqueItems();
        foreach (var person in SeedCatalogue.People)
        {
            hashes.Should().NotContain(person.IdNumber, "rule 10.4 forbids storing the number itself");
        }
    }

    [Fact]
    public async Task Seed_MarksOneCustomerFrozenAndSomeWithADefaultBank()
    {
        await using var database = await fixture.CreateCoreAsync();
        await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        (await database.Customers.CountAsync(c => c.Status == CustomerStatus.Frozen))
            .Should().BeGreaterThan(0, "the portal freeze flow needs someone to demonstrate on");

        (await database.Customers.CountAsync(c => c.DefaultLinkedAccountId != null))
            .Should().BeGreaterThan(0, "FR-08 needs a customer whose picker is skipped");

        (await database.Customers.CountAsync(c => c.DefaultLinkedAccountId == null))
            .Should().BeGreaterThan(0, "and one whose picker is shown");
    }

    [Fact]
    public async Task Seed_CanBeRunTwiceWithoutDuplicating()
    {
        await using var database = await fixture.CreateCoreAsync();
        var seeder = new CoreSeeder(database, Hasher, new FixedClock());

        await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        second.AlreadySeeded.Should().BeTrue();
        (await database.Customers.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task Seed_WritesAnAuditRowThatVerifies()
    {
        await using var database = await fixture.CreateCoreAsync();
        await new CoreSeeder(database, Hasher, new FixedClock()).SeedAsync();

        var result = await new FirmlyPaid.Data.Core.AuditChainVerifier(database).VerifyAsync();

        result.IsIntact.Should().BeTrue();
        result.RowsChecked.Should().Be(1);
    }

    [Fact]
    public async Task Clear_EmptiesEverySeededTable()
    {
        await using var database = await fixture.CreateCoreAsync();
        var seeder = new CoreSeeder(database, Hasher, new FixedClock());
        await seeder.SeedAsync();

        await seeder.ClearAsync();

        (await database.Customers.CountAsync()).Should().Be(0);
        (await database.LinkedAccounts.CountAsync()).Should().Be(0);
        (await database.Merchants.CountAsync()).Should().Be(0);
        (await database.Terminals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void EverySeededIdNumberIsGenuinelyValid()
    {
        // The Home Affairs simulator in step 3 will reject anything with a bad checksum,
        // so the seed must not lean on numbers that would never pass FR-03.
        foreach (var person in SeedCatalogue.People)
        {
            SouthAfricanIdNumber.IsValid(person.IdNumber)
                .Should().BeTrue($"{person.FullName} must have a valid ID number");
        }
    }
}
