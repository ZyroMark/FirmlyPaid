namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Time, injected so tests can move it. Every stored time is UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
