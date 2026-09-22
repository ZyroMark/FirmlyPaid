namespace FirmlyPaid.Data.Core.Entities;

/// <summary>
/// A message the SMS simulator sent, kept so an admin page can show a demo the one-time
/// code without a SIM card (part 7). Only the masked number is stored.
/// </summary>
public class SmsMessage
{
    public Guid SmsMessageId { get; set; } = Guid.NewGuid();

    public required string MaskedCellphoneNumber { get; set; }

    public required string Message { get; set; }

    public DateTime SentAt { get; set; }
}
