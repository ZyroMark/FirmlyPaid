namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// The fingerprint reader used once at enrolment for the Home Affairs check.
/// Separate from the vein scanner: different device, different purpose.
/// </summary>
public interface IFingerprintScanner
{
    Task<FingerprintCaptureResult> CaptureAsync(CancellationToken ct = default);
}

/// <param name="Sample">Opaque bytes passed straight to the Home Affairs verifier; never stored by us.</param>
/// <param name="QualityScore">0 to 100.</param>
public sealed record FingerprintCaptureResult(byte[]? Sample, int QualityScore)
{
    public bool IsUsable => Sample is { Length: > 0 };
}
