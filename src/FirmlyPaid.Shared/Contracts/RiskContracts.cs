using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>POST /risk/score. Internal only.</summary>
public sealed record RiskScoreRequest(
    Guid CustomerId,
    Guid TerminalId,
    decimal Amount,
    int PaymentsInLastWindow,
    int FailedMatchAttempts);

/// <param name="Score">0 (no concern) to 100 (block).</param>
/// <param name="Reasons">Rule names that fired, for the RiskEvents table and admin alerts.</param>
public sealed record RiskScoreResponse(int Score, RiskAction Action, IReadOnlyList<string> Reasons);
