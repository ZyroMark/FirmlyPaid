using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Matching.Api.Tests;

/// <summary>
/// The vault half of step 4: templates are encrypted and transformed before storage, a
/// match searches one bucket only, and a revoked template stops matching (rules 10.1 to
/// 10.3, and the groundwork for FR-06 and FR-07).
/// </summary>
[Collection(nameof(MatchingCollection))]
public class TemplateVaultTests(MatchingTestHost host)
{
    private const int SamplesPerFinger = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Each test works in its own bucket, so one test's candidates never widen another's
    // search. Real buckets are ID digits; these are simply unused values.
    private static int _nextBucket = 1000;

    private static int NextBucket() => Interlocked.Increment(ref _nextBucket);

    [Fact]
    public async Task TheSameFingerMatchesItself()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        var result = await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, bucket);

        result.Matched.Should().BeTrue();
        result.TemplateOwnerId.Should().Be(owner);
        result.Score.Should().BeGreaterThan(0.85);
    }

    [Fact]
    public async Task ADifferentFingerDoesNotMatch()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        // Same bucket, different person: the four digits alone must never be enough.
        var result = await MatchAsync("Sipho Dlamini", FingerPosition.RightIndex, bucket);

        result.Matched.Should().BeFalse();
        result.TemplateOwnerId.Should().BeNull();
        result.CandidatesSearched.Should().Be(1);
    }

    [Fact]
    public async Task TheSamePersonsOtherFingerIsStillTheirs()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);
        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.LeftIndex);

        var result = await MatchAsync("Thandiwe Mokoena", FingerPosition.LeftIndex, bucket);

        result.Matched.Should().BeTrue();
        result.TemplateOwnerId.Should().Be(owner);
    }

    [Fact]
    public async Task FR07_AMatchSearchesOnlyItsOwnBucket()
    {
        var searchedBucket = NextBucket();
        var otherBucket = NextBucket();

        await StoreAsync(Guid.NewGuid(), searchedBucket, "Thandiwe Mokoena", FingerPosition.RightIndex);
        await StoreAsync(Guid.NewGuid(), searchedBucket, "Sipho Dlamini", FingerPosition.RightIndex);

        // Three more people who share nothing but the estate they are stored in.
        await StoreAsync(Guid.NewGuid(), otherBucket, "Naledi Sithole", FingerPosition.RightIndex);
        await StoreAsync(Guid.NewGuid(), otherBucket, "Pieter Coetzee", FingerPosition.RightIndex);
        await StoreAsync(Guid.NewGuid(), otherBucket, "Elsie Jacobs", FingerPosition.RightIndex);

        var result = await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, searchedBucket);

        result.Matched.Should().BeTrue();

        // The count is the evidence: the other bucket was never opened.
        result.CandidatesSearched.Should().Be(2);
    }

    [Fact]
    public async Task TheRightFingerInTheWrongBucketDoesNotMatch()
    {
        var bucket = NextBucket();
        await StoreAsync(Guid.NewGuid(), bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        var result = await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, NextBucket());

        result.Matched.Should().BeFalse();
        result.CandidatesSearched.Should().Be(0);
    }

    [Fact]
    public async Task NothingResemblingTheReadingIsWrittenToTheVault()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        // What the sensor would have produced, if anyone were careless enough to store it.
        var reading = SimulatedPattern("Thandiwe Mokoena", FingerPosition.RightIndex);

        await using var vault = host.OpenVault();
        var stored = await vault.VeinTemplates.Where(t => t.TemplateOwnerId == owner).ToListAsync();

        stored.Should().HaveCount(SamplesPerFinger);

        foreach (var template in stored)
        {
            template.EncryptedTemplate.Should().NotEqual(reading);

            // AES-GCM adds a nonce and a tag, so ciphertext is never the same length.
            template.EncryptedTemplate.Length.Should().BeGreaterThan(reading.Length);
            template.KeyVersion.Should().NotBeNullOrWhiteSpace();
            template.TransformSeedId.Should().NotBeEmpty();
        }

        // Every sample of one finger shares the customer's transform (rule 10.3).
        stored.Select(t => t.TransformSeedId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task ARevokedTemplateStopsMatching()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);
        (await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, bucket)).Matched.Should().BeTrue();

        var revoked = await PostAsync<RevokeTemplatesResponse>($"/templates/{owner}/revoke");
        revoked.TemplatesRevoked.Should().Be(SamplesPerFinger);

        var afterRevoke = await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, bucket);

        afterRevoke.Matched.Should().BeFalse();
        afterRevoke.CandidatesSearched.Should().Be(1);
    }

    [Fact]
    public async Task ReEnrolmentAfterARevocationUsesAFreshTransform()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        await using var vault = host.OpenVault();
        var firstSeed = await vault.VeinTemplates
            .Where(t => t.TemplateOwnerId == owner)
            .Select(t => t.TransformSeedId)
            .FirstAsync();

        await PostAsync<RevokeTemplatesResponse>($"/templates/{owner}/revoke");
        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        var secondSeed = await vault.VeinTemplates
            .Where(t => t.TemplateOwnerId == owner && t.RevokedAt == null)
            .Select(t => t.TransformSeedId)
            .FirstAsync();

        // The point of a cancellable template: the new one is a different shape, so a copy
        // of the old one is worthless to whoever took it.
        secondSeed.Should().NotBe(firstSeed);

        // And the customer can still pay.
        (await MatchAsync("Thandiwe Mokoena", FingerPosition.RightIndex, bucket)).Matched.Should().BeTrue();
    }

    [Fact]
    public async Task RecapturingAFingerReplacesTheOldTemplates()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);
        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        var summary = await host.Client.GetFromJsonAsync<TemplateSummaryResponse>($"/templates/{owner}", Json);

        summary!.FingersHeld.Should().Be(1);
        summary.LiveTemplates.Should().Be(SamplesPerFinger);
        summary.RevokedTemplates.Should().Be(SamplesPerFinger);
    }

    [Fact]
    public async Task APoorQualityCaptureIsRefusedAndNothingIsStored()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        var samples = await host.ReadFingerAsync("Thandiwe Mokoena", FingerPosition.RightIndex, SamplesPerFinger);

        var response = await PostAsync<StoreTemplatesResponse>("/templates", new StoreTemplatesRequest(
            owner,
            bucket,
            FingerPosition.RightIndex,
            samples,
            // One blurred read in three is enough to send the agent round again.
            QualityScores: [92, 41, 90]));

        response.Accepted.Should().BeFalse();
        response.RejectionReason.Should().Contain("again");

        await using var vault = host.OpenVault();
        (await vault.VeinTemplates.CountAsync(t => t.TemplateOwnerId == owner)).Should().Be(0);
    }

    [Fact]
    public async Task DeletingAnOwnerRemovesEveryTemplateAndTheirBucket()
    {
        var owner = Guid.NewGuid();
        var bucket = NextBucket();

        await StoreAsync(owner, bucket, "Thandiwe Mokoena", FingerPosition.RightIndex);

        var deleted = await host.Client.DeleteAsync($"/templates/{owner}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var vault = host.OpenVault();
        (await vault.VeinTemplates.CountAsync(t => t.TemplateOwnerId == owner)).Should().Be(0);
        (await vault.Buckets.CountAsync(b => b.TemplateOwnerId == owner)).Should().Be(0);
    }

    [Fact]
    public async Task TheWrongNumberOfSamplesIsRefusedWithACode()
    {
        var samples = await host.ReadFingerAsync("Thandiwe Mokoena", FingerPosition.RightIndex, 2);

        var response = await host.Client.PostAsJsonAsync("/templates", new StoreTemplatesRequest(
            Guid.NewGuid(),
            NextBucket(),
            FingerPosition.RightIndex,
            samples,
            QualityScores: [90, 90]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Shared.Errors.ApiError>(Json);
        error!.Code.Should().Be(Shared.Errors.ErrorCodes.ValidationError);
        error.Message.Should().Contain("3 samples");
    }

    private static byte[] SimulatedPattern(string label, FingerPosition position) =>
        Simulators.Vein.SimulatedFingerCatalogue.PatternFor(label, position);

    private async Task StoreAsync(Guid owner, int bucket, string label, FingerPosition position)
    {
        var samples = await host.ReadFingerAsync(label, position, SamplesPerFinger);

        var response = await PostAsync<StoreTemplatesResponse>("/templates", new StoreTemplatesRequest(
            owner,
            bucket,
            position,
            samples,
            QualityScores: [.. samples.Select(_ => 92)]));

        response.Accepted.Should().BeTrue();
    }

    private async Task<MatchResponse> MatchAsync(string label, FingerPosition position, int bucket)
    {
        var probe = await host.ReadFingerAsync(label, position);

        return await PostAsync<MatchResponse>("/match", new MatchRequest(probe, bucket));
    }

    private async Task<TResponse> PostAsync<TResponse>(string path, object? body = null)
    {
        var response = body is null
            ? await host.Client.PostAsync(path, content: null)
            : await host.Client.PostAsJsonAsync(path, body, Json);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TResponse>(Json))!;
    }
}
