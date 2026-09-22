namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// One message the SMS simulator sent. The number is masked, because a log line and an
/// admin page are both places a full cellphone number should not appear.
/// </summary>
public sealed record SentSms(string MaskedCellphoneNumber, string Message, DateTime SentAtUtc);

/// <summary>
/// Where sent messages are kept so a demo can read a one-time code without a SIM card.
/// The interface lives here so the database-backed store and the in-memory one can be
/// swapped without either side knowing about the other.
/// </summary>
public interface ISentSmsLog
{
    Task AddAsync(SentSms message, CancellationToken ct = default);

    /// <summary>Most recent first.</summary>
    Task<IReadOnlyList<SentSms>> RecentAsync(int count = 50, CancellationToken ct = default);

    Task ClearAsync(CancellationToken ct = default);
}
