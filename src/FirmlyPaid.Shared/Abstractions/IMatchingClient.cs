using FirmlyPaid.Shared.Contracts;

namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// How the other services talk to the Matching service. Everything biometric goes through
/// here, because Matching is the only service allowed to read FirmlyPaidVault (rule 10.2).
/// </summary>
public interface IMatchingClient
{
    Task<StoreTemplatesResponse> StoreTemplatesAsync(StoreTemplatesRequest request, CancellationToken ct = default);

    Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken ct = default);

    Task<TemplateSummaryResponse> GetSummaryAsync(Guid templateOwnerId, CancellationToken ct = default);

    Task<RevokeTemplatesResponse> RevokeAsync(Guid templateOwnerId, CancellationToken ct = default);

    Task<DeleteTemplatesResponse> DeleteAsync(Guid templateOwnerId, CancellationToken ct = default);
}
