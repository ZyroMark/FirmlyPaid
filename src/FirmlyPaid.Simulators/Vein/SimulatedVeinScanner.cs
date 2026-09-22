using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.Vein;

/// <summary>
/// Stands in for the Hitachi finger vein unit. The operator chooses a test finger and a
/// button on the panel; this turns that into the same result a real scanner would give.
/// </summary>
/// <remarks>
/// Like the real adapter, this is the only place unencrypted sensor data exists. The
/// template is encrypted before it is returned, so nothing downstream can accidentally
/// handle a raw one (rule 10.1).
/// </remarks>
public sealed class SimulatedVeinScanner(
    SimulatorControlState control,
    IKeyVault keyVault,
    ILogger<SimulatedVeinScanner> logger) : IVeinScanner
{
    /// <summary>Which test finger the panel currently has selected.</summary>
    public SimulatedFingerCatalogue.SimulatedFinger SelectedFinger { get; set; } =
        SimulatedFingerCatalogue.All[0];

    public async Task<VeinCaptureResult> CaptureAsync(VeinCaptureRequest request, CancellationToken ct = default)
    {
        switch (control.VeinScanner)
        {
            case VeinScannerMode.NoFinger:
                logger.LogInformation("Simulated scanner saw no finger");
                return VeinCaptureResult.Failed(VeinCaptureOutcome.NoFinger);

            case VeinScannerMode.FakeFinger:
                // A real sensor checks blood flow. A printed or moulded finger fails here
                // and no template is produced at all.
                logger.LogInformation("Simulated scanner rejected a fake finger");
                return VeinCaptureResult.Failed(VeinCaptureOutcome.LivenessFailed);
        }

        var poorQuality = control.VeinScanner == VeinScannerMode.PoorQuality;

        // A poor read is a noisy read: more variation from the stored pattern, and a
        // quality score the enrolment rules will turn away.
        var noise = poorQuality ? Math.Max(control.VeinNoiseAmplitude * 6, 40) : control.VeinNoiseAmplitude;

        var label = SelectedFinger.Label;
        var position = request.FingerPosition == FingerPosition.Unknown
            ? SelectedFinger.Position
            : request.FingerPosition;

        var reading = Read(SimulatedFingerCatalogue.PatternFor(label, position), noise);
        var quality = QualityFor(noise);

        var encrypted = await keyVault.ProtectAsync(reading, ct);

        logger.LogInformation(
            "Simulated scanner read a {Position} finger with quality {Quality}",
            position,
            quality);

        return new VeinCaptureResult(
            poorQuality ? VeinCaptureOutcome.PoorQuality : VeinCaptureOutcome.Good,
            encrypted,
            quality);
    }

    /// <summary>
    /// Adds a little random noise to the stored pattern. No two reads of the same finger
    /// are identical, so matching has to score similarity rather than compare bytes.
    /// </summary>
    private static byte[] Read(byte[] pattern, int noiseAmplitude)
    {
        if (noiseAmplitude == 0)
        {
            return pattern;
        }

        var reading = new byte[pattern.Length];

        for (var i = 0; i < pattern.Length; i++)
        {
            var drift = Random.Shared.Next(-noiseAmplitude, noiseAmplitude + 1);
            reading[i] = (byte)Math.Clamp(pattern[i] + drift, 0, 255);
        }

        return reading;
    }

    /// <summary>A clean read scores in the nineties; a noisy one falls below the threshold.</summary>
    private static int QualityFor(int noiseAmplitude) =>
        Math.Clamp(98 - (noiseAmplitude * 2), 5, 100);
}
