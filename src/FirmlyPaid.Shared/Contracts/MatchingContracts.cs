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
