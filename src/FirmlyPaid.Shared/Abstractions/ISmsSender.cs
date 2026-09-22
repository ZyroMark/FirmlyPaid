namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Sends the one-time codes and notices customers receive. The simulator writes to the
/// console and a table so a demo can read the code without a real SIM.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string cellphoneNumber, string message, CancellationToken ct = default);
}
