using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>
/// POST /match. Internal only: the Matching service is the one place vault data is read.
/// The bucket narrows the search to customers sharing those four ID digits (FR-07).
/// </summary>
public sealed record MatchRequest(EncryptedPayloadDto Probe, int Digits7To10Bucket);

public sealed record MatchResponse(
    bool Matched,
    Guid? TemplateOwnerId,
    double Score,
    int CandidatesSearched);

/// <summary>
/// POST /templates. Enrolment hands over the samples it has just collected; only the
/// Matching service decrypts them, applies the cancellable transform and stores them.
/// </summary>
/// <param name="Digits7To10Bucket">Recorded alongside the templates so a match can find them.</param>
public sealed record StoreTemplatesRequest(
    Guid TemplateOwnerId,
    int Digits7To10Bucket,
    FingerPosition FingerPosition,
    IReadOnlyList<EncryptedPayloadDto> Samples,
    IReadOnlyList<int> QualityScores);

/// <param name="FingersHeld">Distinct finger positions now stored for this owner (FR-01 needs 2).</param>
/// <param name="RejectionReason">Plain English for the agent's screen when Accepted is false.</param>
public sealed record StoreTemplatesResponse(
    bool Accepted,
    int AverageQualityScore,
    int FingersHeld,
    string? RejectionReason);

/// <summary>
/// POST /templates/{templateOwnerId}/revoke. Retires every live template for one person
/// so a leaked copy is worthless; re-enrolment then issues a fresh transform (rule 10.3).
/// </summary>
public sealed record RevokeTemplatesResponse(int TemplatesRevoked);

/// <summary>DELETE /templates/{templateOwnerId}. Hard delete for rule 10.12 and FR-12.</summary>
public sealed record DeleteTemplatesResponse(int TemplatesDeleted);

/// <summary>GET /templates/{templateOwnerId}. What the vault holds, without any template bytes.</summary>
public sealed record TemplateSummaryResponse(
    Guid TemplateOwnerId,
    int FingersHeld,
    int LiveTemplates,
    int RevokedTemplates);
