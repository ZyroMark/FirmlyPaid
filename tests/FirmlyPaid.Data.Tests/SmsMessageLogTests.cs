using FirmlyPaid.Data.Core;
using FirmlyPaid.Shared.Abstractions;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// Part 7: the SMS simulator writes to the console and to a table. This is the table,
/// which an admin page reads so a demo can see the one-time code without a SIM card.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class SmsMessageLogTests(SqlServerFixture fixture)
{
    private static readonly DateTime SentAt = new(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AMessageIsStoredAndReadBack()
    {
        await using var database = await fixture.CreateCoreAsync();
        var log = new SmsMessageLog(database);

        await log.AddAsync(new SentSms("082***0001", "Your FirmlyPaid code is 448120.", SentAt));

        var stored = (await log.RecentAsync()).Should().ContainSingle().Subject;
        stored.Message.Should().Contain("448120");
        stored.MaskedCellphoneNumber.Should().Be("082***0001");
        stored.SentAtUtc.Should().Be(SentAt);
    }

    [Fact]
    public async Task TheMostRecentMessagesComeBackFirst()
    {
        await using var database = await fixture.CreateCoreAsync();
        var log = new SmsMessageLog(database);

        await log.AddAsync(new SentSms("082***0001", "First", SentAt));
        await log.AddAsync(new SentSms("082***0002", "Second", SentAt.AddSeconds(1)));
        await log.AddAsync(new SentSms("082***0003", "Third", SentAt.AddSeconds(2)));

        (await log.RecentAsync(2)).Select(m => m.Message).Should().Equal("Third", "Second");
    }

    [Fact]
    public async Task ClearEmptiesTheTable()
    {
        await using var database = await fixture.CreateCoreAsync();
        var log = new SmsMessageLog(database);
        await log.AddAsync(new SentSms("082***0001", "First", SentAt));

        await log.ClearAsync();

        (await log.RecentAsync()).Should().BeEmpty();
    }
}
