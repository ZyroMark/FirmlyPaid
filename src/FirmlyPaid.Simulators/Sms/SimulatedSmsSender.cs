using System.Collections.Concurrent;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Security;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.Sms;

/// <summary>
/// Keeps the last few hundred messages in memory. Used by tests and by any process that
/// has no database to hand. Services use the table-backed store in FirmlyPaid.Data.Core.
/// </summary>
public sealed class InMemorySentSmsLog : ISentSmsLog
{
    private const int Capacity = 500;

    private readonly ConcurrentQueue<SentSms> _messages = new();

    public Task AddAsync(SentSms message, CancellationToken ct = default)
    {
        _messages.Enqueue(message);

        while (_messages.Count > Capacity && _messages.TryDequeue(out _))
        {
            // Drop the oldest so a long demo cannot grow without bound.
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SentSms>> RecentAsync(int count = 50, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SentSms>>(_messages.Reverse().Take(count).ToList());

    public Task ClearAsync(CancellationToken ct = default)
    {
        while (_messages.TryDequeue(out _))
        {
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Stands in for the SMS provider. Writes each message to the console and to the log the
/// admin console reads (part 7).
/// </summary>
public sealed class SimulatedSmsSender(
    ISentSmsLog log,
    IClock clock,
    ILogger<SimulatedSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(string cellphoneNumber, string message, CancellationToken ct = default)
    {
        var masked = Masking.MaskCellphone(cellphoneNumber);

        await log.AddAsync(new SentSms(masked, message, clock.UtcNow), ct);

        // The message body is shown because a demo needs to read the one-time code. The
        // number is masked, and a real provider adapter would log neither.
        logger.LogInformation("SMS to {MaskedNumber}: {Message}", masked, message);
    }
}
