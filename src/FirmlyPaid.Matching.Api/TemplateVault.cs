using FirmlyPaid.Data.Vault;
using FirmlyPaid.Data.Vault.Entities;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.Matching.Api;

/// <summary>
/// Everything this service does with FirmlyPaidVault. It is the only code in FirmlyPaid
/// that holds a decrypted vein template, and it holds one for the length of a single
/// method call (rules 10.1 and 10.2).
/// </summary>
public sealed class TemplateVault(
    FirmlyPaidVaultDbContext vault,
    IKeyVault keyVault,
    IVeinMatcher matcher,
    IOptions<FirmlyPaidOptions> options,
    IClock clock,
    ILogger<TemplateVault> logger)
{
    private readonly FirmlyPaidOptions _options = options.Value;

    /// <summary>
    /// Stores one finger's samples. They arrive already encrypted from the scanner
    /// adapter; they are decrypted here only to apply the customer's transform, then
    /// encrypted again before they touch the database.
    /// </summary>
    public async Task<StoreTemplatesResponse> StoreAsync(StoreTemplatesRequest request, CancellationToken ct = default)
    {
        Validate(request);

        // The quality gate lives here as well as in Enrolment: the vault will not take a
        // template it would later fail to match (FR-01).
        var worstQuality = request.QualityScores.Min();
        if (worstQuality < _options.Enrolment.MinimumQualityScore)
        {
            logger.LogInformation(
                "Rejected a {Position} capture: quality {Quality} is below the minimum of {Minimum}",
                request.FingerPosition,
                worstQuality,
                _options.Enrolment.MinimumQualityScore);

            return new StoreTemplatesResponse(
                Accepted: false,
                AverageQualityScore: (int)Math.Round(request.QualityScores.Average()),
                FingersHeld: await CountFingersAsync(request.TemplateOwnerId, ct),
                RejectionReason: "That read was not clear enough. Please ask the customer to place the finger again.");
        }

        var transformSeedId = await SeedForAsync(request.TemplateOwnerId, ct);
        var now = clock.UtcNow;

        // Re-capturing a finger replaces what was there, rather than leaving two
        // generations of the same finger to be matched against.
        await RevokeFingerAsync(request.TemplateOwnerId, request.FingerPosition, now, ct);

        for (var i = 0; i < request.Samples.Count; i++)
        {
            var reading = await keyVault.UnprotectAsync(request.Samples[i].ToPayload(), ct);
            var transformed = CancellableTemplateTransform.Apply(reading, transformSeedId);
            var protectedTemplate = await keyVault.ProtectAsync(transformed, ct);

            vault.VeinTemplates.Add(new VeinTemplate
            {
                TemplateOwnerId = request.TemplateOwnerId,
                FingerPosition = request.FingerPosition,
                EncryptedTemplate = protectedTemplate.Ciphertext,
                KeyVersion = protectedTemplate.KeyVersion,
                TransformSeedId = transformSeedId,
                QualityScore = request.QualityScores[i],
                CreatedAt = now,
            });
        }

        await UpsertBucketAsync(request.TemplateOwnerId, request.Digits7To10Bucket, ct);

        await vault.SaveChangesAsync(ct);

        var fingersHeld = await CountFingersAsync(request.TemplateOwnerId, ct);

        logger.LogInformation(
            "Stored {Count} {Position} templates. This owner now has {Fingers} finger(s) on file",
            request.Samples.Count,
            request.FingerPosition,
            fingersHeld);

        return new StoreTemplatesResponse(
            Accepted: true,
            AverageQualityScore: (int)Math.Round(request.QualityScores.Average()),
            FingersHeld: fingersHeld,
            RejectionReason: null);
    }

    /// <summary>
    /// Finds the customer a probe belongs to, searching only the bucket the customer typed
    /// on the keypad (FR-07). The returned candidate count is what proves the search did
    /// not widen; the bucket value itself is never logged (rule 10.5).
    /// </summary>
    public async Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken ct = default)
    {
        if (request.Digits7To10Bucket is < 0 or > 9999)
        {
            throw FirmlyPaidException.Validation("The ID digits must be four digits.");
        }

        var owners = await vault.Buckets
            .AsNoTracking()
            .Where(b => b.IdDigits7to10Bucket == request.Digits7To10Bucket)
            .Select(b => b.TemplateOwnerId)
            .ToListAsync(ct);

        if (owners.Count == 0)
        {
            logger.LogInformation("No candidates in that bucket");
            return new MatchResponse(false, null, 0.0, 0);
        }

        var candidates = await vault.VeinTemplates
            .AsNoTracking()
            .Where(t => owners.Contains(t.TemplateOwnerId) && t.RevokedAt == null)
            .Select(t => new
            {
                t.TemplateOwnerId,
                t.TransformSeedId,
                t.EncryptedTemplate,
                t.KeyVersion,
            })
            .ToListAsync(ct);

        var probe = await keyVault.UnprotectAsync(request.Probe.ToPayload(), ct);

        var bestScore = 0.0;
        Guid? bestOwner = null;

        // Grouped by transform because the probe has to be shuffled the same way as the
        // template it is compared with, and that differs per customer.
        foreach (var group in candidates.GroupBy(c => new { c.TemplateOwnerId, c.TransformSeedId }))
        {
            var transformedProbe = CancellableTemplateTransform.Apply(probe, group.Key.TransformSeedId);

            foreach (var candidate in group)
            {
                var stored = await keyVault.UnprotectAsync(
                    new ProtectedPayload(candidate.EncryptedTemplate, candidate.KeyVersion), ct);

                var score = matcher.Compare(transformedProbe, stored);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOwner = group.Key.TemplateOwnerId;
                }
            }
        }

        var matched = bestScore >= _options.Matching.MatchThreshold;

        logger.LogInformation(
            "Searched {Candidates} candidate(s) in one bucket and {Outcome} with a best score of {Score}",
            owners.Count,
            matched ? "matched" : "found no match",
            bestScore.ToString("F3"));

        return new MatchResponse(matched, matched ? bestOwner : null, bestScore, owners.Count);
    }

    /// <summary>What the vault holds for one person. Never returns template bytes.</summary>
    public async Task<TemplateSummaryResponse> SummaryAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var rows = await vault.VeinTemplates
            .AsNoTracking()
            .Where(t => t.TemplateOwnerId == templateOwnerId)
            .Select(t => new { t.FingerPosition, t.RevokedAt })
            .ToListAsync(ct);

        return new TemplateSummaryResponse(
            templateOwnerId,
            FingersHeld: rows.Where(r => r.RevokedAt == null).Select(r => r.FingerPosition).Distinct().Count(),
            LiveTemplates: rows.Count(r => r.RevokedAt == null),
            RevokedTemplates: rows.Count(r => r.RevokedAt != null));
    }

    /// <summary>
    /// Retires every live template for one person (rule 10.3). The rows stay, so the fact
    /// of a revocation is still on record, but they are never matched against again and
    /// re-enrolment starts from a new transform.
    /// </summary>
    public async Task<RevokeTemplatesResponse> RevokeAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var revoked = await vault.VeinTemplates
            .Where(t => t.TemplateOwnerId == templateOwnerId && t.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        logger.LogInformation("Revoked {Count} template(s) for one owner", revoked);

        return new RevokeTemplatesResponse(revoked);
    }

    /// <summary>
    /// Deletes everything the vault holds for one person (rule 10.12). A hard delete in
    /// this build, so the customer must re-enrol from scratch afterwards (FR-12).
    /// </summary>
    public async Task<DeleteTemplatesResponse> DeleteAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var deleted = await vault.VeinTemplates
            .Where(t => t.TemplateOwnerId == templateOwnerId)
            .ExecuteDeleteAsync(ct);

        await vault.Buckets
            .Where(b => b.TemplateOwnerId == templateOwnerId)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation("Deleted {Count} template(s) for one owner", deleted);

        return new DeleteTemplatesResponse(deleted);
    }

    private void Validate(StoreTemplatesRequest request)
    {
        if (request.TemplateOwnerId == Guid.Empty)
        {
            throw FirmlyPaidException.Validation("A template owner is required.");
        }

        if (request.FingerPosition == FingerPosition.Unknown)
        {
            throw FirmlyPaidException.Validation("The finger position is required.");
        }

        if (request.Digits7To10Bucket is < 0 or > 9999)
        {
            throw FirmlyPaidException.Validation("The ID digits must be four digits.");
        }

        var expected = _options.Enrolment.SamplesPerFinger;
        if (request.Samples.Count != expected)
        {
            throw FirmlyPaidException.Validation($"Exactly {expected} samples are required for each finger.");
        }

        if (request.QualityScores.Count != request.Samples.Count)
        {
            throw FirmlyPaidException.Validation("Every sample needs a quality score.");
        }
    }

    /// <summary>
    /// The transform a customer already uses, or a new one. Every template a person has
    /// shares a seed, so a probe is shuffled once per candidate rather than once per row.
    /// </summary>
    private async Task<Guid> SeedForAsync(Guid templateOwnerId, CancellationToken ct)
    {
        var existing = await vault.VeinTemplates
            .AsNoTracking()
            .Where(t => t.TemplateOwnerId == templateOwnerId && t.RevokedAt == null)
            .Select(t => (Guid?)t.TransformSeedId)
            .FirstOrDefaultAsync(ct);

        return existing ?? CancellableTemplateTransform.NewSeed();
    }

    /// <summary>
    /// Retires the previous capture of one finger. Tracked rather than a bulk update, so
    /// the revocation and the replacement templates are saved in the same transaction and
    /// a customer can never be left with neither.
    /// </summary>
    private async Task RevokeFingerAsync(Guid templateOwnerId, FingerPosition position, DateTime now, CancellationToken ct)
    {
        var previous = await vault.VeinTemplates
            .Where(t => t.TemplateOwnerId == templateOwnerId
                     && t.FingerPosition == position
                     && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var template in previous)
        {
            template.RevokedAt = now;
        }
    }

    private async Task<int> CountFingersAsync(Guid templateOwnerId, CancellationToken ct) =>
        await vault.VeinTemplates
            .AsNoTracking()
            .Where(t => t.TemplateOwnerId == templateOwnerId && t.RevokedAt == null)
            .Select(t => t.FingerPosition)
            .Distinct()
            .CountAsync(ct);

    private async Task UpsertBucketAsync(Guid templateOwnerId, int bucket, CancellationToken ct)
    {
        var existing = await vault.Buckets.FirstOrDefaultAsync(b => b.TemplateOwnerId == templateOwnerId, ct);

        if (existing is null)
        {
            vault.Buckets.Add(new Bucket { TemplateOwnerId = templateOwnerId, IdDigits7to10Bucket = bucket });
        }
        else
        {
            existing.IdDigits7to10Bucket = bucket;
        }
    }
}
