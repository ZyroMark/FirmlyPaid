using FirmlyPaid.Data.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// FR-13: every admin action writes an audit row, and tampering with one row is detected
/// by the chain check. The chain is built in step 2 so later steps can just append to it.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class AuditLogTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AnUntouchedChainVerifies()
    {
        await using var database = await fixture.CreateCoreAsync();
        await AppendThreeRowsAsync(database);

        var result = await new AuditChainVerifier(database).VerifyAsync();

        result.IsIntact.Should().BeTrue();
        result.RowsChecked.Should().Be(3);
        result.BrokenAtSequence.Should().BeNull();
    }

    [Fact]
    public async Task ChangingARowIsDetected()
    {
        await using var database = await fixture.CreateCoreAsync();
        await AppendThreeRowsAsync(database);

        // Edit the middle row straight in the table, the way an attacker with database
        // access would. Its stored hash no longer matches its contents.
        await database.Database.ExecuteSqlAsync(
            $"UPDATE AuditLog SET Details = 'nothing to see here' WHERE Sequence = 2");

        var result = await new AuditChainVerifier(database).VerifyAsync();

        result.IsIntact.Should().BeFalse();
        result.BrokenAtSequence.Should().Be(2);
        result.Reason.Should().Contain("changed");
    }

    [Fact]
    public async Task DeletingARowIsDetected()
    {
        await using var database = await fixture.CreateCoreAsync();
        await AppendThreeRowsAsync(database);

        await database.Database.ExecuteSqlAsync($"DELETE FROM AuditLog WHERE Sequence = 2");

        var result = await new AuditChainVerifier(database).VerifyAsync();

        result.IsIntact.Should().BeFalse();
        // Row 3 is now the one that does not follow on from what precedes it.
        result.BrokenAtSequence.Should().Be(3);
    }

    [Fact]
    public async Task AuditDetailsCannotCarryAnIdNumber()
    {
        // Rule 10.5: the writer refuses rather than trusting every caller to remember.
        await using var database = await fixture.CreateCoreAsync();

        var act = async () => await database.AppendAuditAsync(
            actor: "admin@zyromark.co.za",
            action: "CustomerViewed",
            entityType: "Customer",
            entityId: Guid.NewGuid().ToString(),
            details: "Viewed customer 8001015009087",
            createdAtUtc: DateTime.UtcNow);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*never be written down*");
    }

    [Fact]
    public async Task SequenceIsAssignedByTheDatabaseInWriteOrder()
    {
        await using var database = await fixture.CreateCoreAsync();
        await AppendThreeRowsAsync(database);

        var sequences = await database.AuditLog.OrderBy(a => a.Sequence).Select(a => a.Sequence).ToListAsync();

        sequences.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    private static async Task AppendThreeRowsAsync(FirmlyPaidCoreDbContext database)
    {
        var at = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);

        // Saved one at a time: each row must chain off the one already committed.
        for (var i = 1; i <= 3; i++)
        {
            await database.AppendAuditAsync(
                actor: "admin@zyromark.co.za",
                action: "MerchantUpdated",
                entityType: "Merchant",
                entityId: $"merchant-{i}",
                details: $"Changed the fee percent on merchant {i}.",
                createdAtUtc: at.AddMinutes(i));

            await database.SaveChangesAsync();
        }
    }
}
