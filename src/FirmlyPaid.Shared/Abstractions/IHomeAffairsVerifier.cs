using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Checks a person's identity against Home Affairs during enrolment.
/// The simulator answers from a seeded list; the real adapter calls a verification partner.
/// </summary>
public interface IHomeAffairsVerifier
{
    Task<HomeAffairsVerificationResult> VerifyAsync(
        HomeAffairsVerificationRequest request,
        CancellationToken ct = default);
}

/// <param name="IdNumber">The 13 digit South African ID number. Never persisted or logged in full.</param>
/// <param name="FingerprintSample">The sample captured by <see cref="IFingerprintScanner"/>.</param>
public sealed record HomeAffairsVerificationRequest(
    string IdNumber,
    string FullName,
    byte[] FingerprintSample);

/// <param name="Reference">The partner's reference for this check, safe to store and audit.</param>
public sealed record HomeAffairsVerificationResult(HomeAffairsOutcome Outcome, string Reference)
{
    public bool IsMatch => Outcome == HomeAffairsOutcome.Match;
}
