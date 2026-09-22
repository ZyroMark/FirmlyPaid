using FirmlyPaid.Data.Core.Entities;
using FirmlyPaid.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.Data.Core;

/// <summary>
/// The table behind the SMS simulator. A real provider adapter would keep no message
/// bodies at all; this exists only so a demo can read a one-time code off an admin page.
/// </summary>
public sealed class SmsMessageLog(FirmlyPaidCoreDbContext database) : ISentSmsLog
{
    public async Task AddAsync(SentSms message, CancellationToken ct = default)
    {
        database.SmsMessages.Add(new SmsMessage
        {
            MaskedCellphoneNumber = message.MaskedCellphoneNumber,
            Message = message.Message,
            SentAt = message.SentAtUtc,
        });

        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SentSms>> RecentAsync(int count = 50, CancellationToken ct = default) =>
        await database.SmsMessages
            .AsNoTracking()
            .OrderByDescending(m => m.SentAt)
            .Take(count)
            .Select(m => new SentSms(m.MaskedCellphoneNumber, m.Message, m.SentAt))
            .ToListAsync(ct);

    public async Task ClearAsync(CancellationToken ct = default) =>
        await database.SmsMessages.ExecuteDeleteAsync(ct);
}
