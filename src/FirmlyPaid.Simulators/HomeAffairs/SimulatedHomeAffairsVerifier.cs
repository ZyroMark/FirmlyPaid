using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Simulators.HomeAffairs;

/// <summary>
/// Stands in for the Home Affairs verification partner. Answers from the roster of 20 test
/// identities (part 7), or from whatever outcome the demo switchboard is forcing.
/// </summary>
public sealed class SimulatedHomeAffairsVerifier(
    SimulatorControlState control,
    ILogger<SimulatedHomeAffairsVerifier> logger) : IHomeAffairsVerifier
{
    public Task<HomeAffairsVerificationResult> VerifyAsync(
        HomeAffairsVerificationRequest request,
        CancellationToken ct = default)
    {
        // A number with a bad checksum never reaches the partner. FR-03 requires the
        // check before the call, and the real partner would charge us for it anyway.
        if (!SouthAfricanIdNumber.IsValid(request.IdNumber))
        {
            throw new ArgumentException(
                "The ID number must be checked with SouthAfricanIdNumber.IsValid before calling Home Affairs.",
                nameof(request));
        }

        if (request.FingerprintSample.Length == 0)
        {
            throw new ArgumentException("A fingerprint sample is required.", nameof(request));
        }

        var reference = BuildReference(request.IdNumber);

        var forced = control.HomeAffairsOverride;
        if (forced is not null)
        {
            logger.LogInformation("Home Affairs simulator forced to {Outcome} by the demo switchboard", forced);
            return Task.FromResult(new HomeAffairsVerificationResult(forced.Value, reference));
        }

        var entry = HomeAffairsRoster.Find(request.IdNumber);

        // Someone who is not on the roster is someone Home Affairs has never heard of.
        var outcome = entry?.Outcome ?? HomeAffairsOutcome.NoMatch;

        logger.LogInformation("Home Affairs simulator answered {Outcome} with reference {Reference}", outcome, reference);

        return Task.FromResult(new HomeAffairsVerificationResult(outcome, reference));
    }

    /// <summary>
    /// A reference that is stable per identity and safe to store. It is derived from the
    /// ID number rather than containing it, so an audit row can quote it (rule 10.5).
    /// </summary>
    private static string BuildReference(string idNumber)
    {
        var digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"home-affairs-simulator:{idNumber}"));

        var hex = Convert.ToHexStringLower(digest.AsSpan(0, 6));

        // Grouped in fours so the reference can never contain a long run of digits. An
        // unbroken run would look like an account number to the audit writer's check
        // (rule 10.5) and would stop a perfectly good enrolment from being recorded.
        return $"HA-SIM-{hex[..4]}-{hex[4..8]}-{hex[8..]}";
    }
}
