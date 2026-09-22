using System.Security.Cryptography;
using System.Text;
using FirmlyPaid.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.Fingerprint;

/// <summary>
/// Stands in for the Suprema BioMini reader, used once at enrolment for the Home Affairs
/// check. The sample is passed straight through to the verifier and never stored by us,
/// so it only has to be stable per test person.
/// </summary>
public sealed class SimulatedFingerprintScanner(
    SimulatorControlState control,
    ILogger<SimulatedFingerprintScanner> logger) : IFingerprintScanner
{
    private const int SampleLength = 128;

    /// <summary>Which test person the panel has selected. Set by the enrolment app.</summary>
    public string SelectedPersonLabel { get; set; } = "unspecified";

    public Task<FingerprintCaptureResult> CaptureAsync(CancellationToken ct = default)
    {
        // The fingerprint reader has no liveness check of its own, so it reuses the
        // scanner panel's "no finger" button and nothing else.
        if (control.VeinScanner == VeinScannerMode.NoFinger)
        {
            logger.LogInformation("Simulated fingerprint reader saw no finger");
            return Task.FromResult(new FingerprintCaptureResult(null, 0));
        }

        var sample = SampleFor(SelectedPersonLabel);
        logger.LogInformation("Simulated fingerprint reader captured a sample");

        return Task.FromResult(new FingerprintCaptureResult(sample, 88));
    }

    /// <summary>Stable bytes per person, derived rather than stored in a lookup table.</summary>
    public static byte[] SampleFor(string personLabel)
    {
        var sample = new byte[SampleLength];
        var block = SHA512.HashData(Encoding.UTF8.GetBytes($"firmlypaid-simulated-fingerprint:{personLabel}"));
        var written = 0;

        while (written < SampleLength)
        {
            var take = Math.Min(block.Length, SampleLength - written);
            block.AsSpan(0, take).CopyTo(sample.AsSpan(written));
            written += take;
            block = SHA512.HashData(block);
        }

        return sample;
    }
}
