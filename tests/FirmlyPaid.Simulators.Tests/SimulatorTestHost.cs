using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Simulators.Keys;
using Microsoft.Extensions.Logging.Abstractions;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// Shared plumbing for the simulator tests: a key vault backed by a throwaway file, and a
/// clock the tests can move so nothing has to wait five seconds for a bank confirmation.
/// </summary>
public sealed class SimulatorTestHost : IDisposable
{
    private readonly string _keyFilePath = Path.Combine(Path.GetTempPath(), $"firmlypaid-test-{Guid.NewGuid():N}.devkey");

    public SimulatorTestHost()
    {
        KeyVault = new LocalDevFileKeyVault(_keyFilePath, NullLogger<LocalDevFileKeyVault>.Instance);
    }

    public SimulatorControlState Control { get; } = new();

    public LocalDevFileKeyVault KeyVault { get; }

    public TestClock Clock { get; } = new();

    public void Dispose()
    {
        if (File.Exists(_keyFilePath))
        {
            File.Delete(_keyFilePath);
        }
    }
}

/// <summary>A clock the test moves by hand, so a five second wait costs nothing.</summary>
public sealed class TestClock : IClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
