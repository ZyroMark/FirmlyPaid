using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.Data.Core;

/// <summary>
/// Walks the audit log in order and recomputes every hash. If a row was edited or removed,
/// its own hash or the next row's PreviousHash stops matching and this says where (FR-13).
/// </summary>
public sealed class AuditChainVerifier(FirmlyPaidCoreDbContext database)
{
    /// <param name="BrokenAtSequence">The first row that does not verify, or null when the chain is intact.</param>
    public sealed record VerificationResult(bool IsIntact, int RowsChecked, long? BrokenAtSequence, string? Reason);

    public async Task<VerificationResult> VerifyAsync(CancellationToken ct = default)
    {
        var rows = await database.AuditLog
            .AsNoTracking()
            .OrderBy(a => a.Sequence)
            .ToListAsync(ct);

        var expectedPreviousHash = AuditHash.GenesisHash;

        foreach (var row in rows)
        {
            if (row.PreviousHash != expectedPreviousHash)
            {
                return new VerificationResult(
                    false,
                    rows.Count,
                    row.Sequence,
                    "This row does not follow on from the one before it. A row was changed or removed.");
            }

            var recomputed = AuditHash.Compute(
                row.PreviousHash,
                row.Actor,
                row.Action,
                row.EntityType,
                row.EntityId,
                row.Details,
                row.CreatedAt);

            if (recomputed != row.Hash)
            {
                return new VerificationResult(
                    false,
                    rows.Count,
                    row.Sequence,
                    "This row's contents no longer match its hash. The row was changed.");
            }

            expectedPreviousHash = row.Hash;
        }

        return new VerificationResult(true, rows.Count, null, null);
    }
}
