using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;

namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// A finger vein scanner. The adapter is the only place raw sensor data exists: it must
/// return an already-encrypted template and never expose or log an image (rule 10.1).
/// </summary>
public interface IVeinScanner
{
    Task<VeinCaptureResult> CaptureAsync(VeinCaptureRequest request, CancellationToken ct = default);
}

/// <param name="FingerPosition">Which finger the operator asked the customer to present.</param>
/// <param name="TimeoutSeconds">How long to wait for a finger before giving up.</param>
public sealed record VeinCaptureRequest(FingerPosition FingerPosition, int TimeoutSeconds = 10);

/// <param name="Outcome">Good, poor quality, liveness failure, or no finger at all.</param>
/// <param name="EncryptedTemplate">Null unless the outcome is Good. Already encrypted by the adapter.</param>
/// <param name="QualityScore">0 to 100. Enrolment rejects anything below the configured minimum.</param>
public sealed record VeinCaptureResult(
    VeinCaptureOutcome Outcome,
    ProtectedPayload? EncryptedTemplate,
    int QualityScore)
{
    public bool IsUsable => Outcome == VeinCaptureOutcome.Good && EncryptedTemplate is not null;

    public static VeinCaptureResult Failed(VeinCaptureOutcome outcome, int qualityScore = 0) =>
        new(outcome, null, qualityScore);
}
