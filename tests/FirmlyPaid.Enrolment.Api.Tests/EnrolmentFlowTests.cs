using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Enrolment.Api.Tests;

/// <summary>
/// FR-01 to FR-03: capture two fingers with three good samples each, record POPIA consent,
/// and verify the person with Home Affairs before any of it is allowed to happen.
/// </summary>
[Collection(nameof(EnrolmentCollection))]
public class EnrolmentFlowTests(EnrolmentTestHost host) : IAsyncLifetime
{
    private const string ConsentVersion = "POPIA-2026-09-v1";
    private const int SamplesPerFinger = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Every test starts from the same switchboard, whatever the last one forced.</summary>
    public Task InitializeAsync()
    {
        host.Control.Reset();
        host.HomeAffairsCalls.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------------
    // FR-01  Capture 2 fingers, 3 vein samples each, reject low quality
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task FR01_TwoFingersWithThreeGoodSamples_CompletesTheEnrolment()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        var first = await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        first.Accepted.Should().BeTrue();
        first.FingersCaptured.Should().Be(1);

        var second = await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.LeftIndex);
        second.Accepted.Should().BeTrue();
        second.FingersCaptured.Should().Be(2);

        var completed = await CompleteAsync(enrolmentId, "4821");

        completed.Status.Should().Be(CustomerStatus.Active);

        await using var core = host.OpenCore();
        var customer = await core.Customers.SingleAsync(c => c.CustomerId == completed.CustomerId);

        customer.FullName.Should().Be(person.FullName);
        customer.IdDigits7to10Bucket.Should().Be(person.Bucket);
        customer.PinHash.Should().NotBeNullOrWhiteSpace();

        // The vault holds both fingers under the same random handle, and nothing else
        // anywhere ties that handle to a name (rule 10.2).
        var summary = await host.Matching.GetSummaryAsync(customer.TemplateOwnerId);
        summary.FingersHeld.Should().Be(2);
        summary.LiveTemplates.Should().Be(SamplesPerFinger * 2);
    }

    [Fact]
    public async Task FR01_APoorQualitySample_IsRejectedWithARetryMessage()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        // The scanner panel's "poor quality" button: a real, blurred read.
        host.Control.VeinScanner = VeinScannerMode.PoorQuality;

        var response = await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);

        response.Accepted.Should().BeFalse();
        response.RetryMessage.Should().Contain("try the same finger again");
        response.FingersCaptured.Should().Be(0);

        // And nothing was written: a blurred template would only fail at a till later.
        await using var core = host.OpenCore();
        var enrolment = await core.Enrolments.SingleAsync(e => e.EnrolmentId == enrolmentId);
        var summary = await host.Matching.GetSummaryAsync(enrolment.TemplateOwnerId);
        summary.LiveTemplates.Should().Be(0);
    }

    [Fact]
    public async Task FR01_AGoodReadAfterAPoorOne_IsAccepted()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        host.Control.VeinScanner = VeinScannerMode.PoorQuality;
        (await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex)).Accepted.Should().BeFalse();

        host.Control.VeinScanner = VeinScannerMode.GoodRead;
        var retry = await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);

        retry.Accepted.Should().BeTrue();
        retry.FingersCaptured.Should().Be(1);
    }

    [Fact]
    public async Task FR01_OneFinger_CannotCompleteTheEnrolment()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);

        var error = await CompleteExpectingErrorAsync(enrolmentId, "4821");

        error.Code.Should().Be(ErrorCodes.EnrolmentIncomplete);
        error.Message.Should().Contain("2 different fingers");
    }

    [Fact]
    public async Task FR01_TheSameFingerTwice_StillCountsAsOneFinger()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        var again = await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);

        again.Accepted.Should().BeTrue();
        again.FingersCaptured.Should().Be(1);

        (await CompleteExpectingErrorAsync(enrolmentId, "4821")).Code.Should().Be(ErrorCodes.EnrolmentIncomplete);
    }

    [Fact]
    public async Task FR01_TwoSamplesInsteadOfThree_IsRefused()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        var samples = await host.ReadFingerAsync(person.FullName, FingerPosition.RightIndex, 2);

        var response = await host.Client.PostAsJsonAsync(
            $"/enrolments/{enrolmentId}/vein-samples",
            new SubmitVeinSamplesRequest(
                FingerPosition.RightIndex,
                [.. samples.Select(s => s.Payload)],
                [.. samples.Select(s => s.QualityScore)]),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorFrom(response)).Code.Should().Be(ErrorCodes.ValidationError);
    }

    // -----------------------------------------------------------------------------
    // FR-02  Record POPIA consent
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task FR02_WithoutConsent_EnrolmentFails()
    {
        var person = AnUnenrolledPerson();

        var response = await PostEnrolmentAsync(person, consentAccepted: false);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var error = await ErrorFrom(response);
        error.Code.Should().Be(ErrorCodes.ConsentRequired);

        // Refused before the partner was troubled, and before anything was recorded.
        host.HomeAffairsCalls.Calls.Should().Be(0);
    }

    [Fact]
    public async Task FR02_TheConsentRow_StoresTheVersionAgentAndTime()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.LeftIndex);

        var completed = await CompleteAsync(enrolmentId, "4821");

        await using var core = host.OpenCore();
        var consent = await core.Consents.SingleAsync(c => c.CustomerId == completed.CustomerId);

        consent.ConsentTextVersion.Should().Be(ConsentVersion);
        consent.AgentId.Should().Be(SeedCatalogue.DemoAgentId);
        consent.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        consent.WithdrawnAt.Should().BeNull();

        // The acceptance is dated when the customer agreed, not when the profile went live.
        var enrolment = await core.Enrolments.SingleAsync(e => e.EnrolmentId == enrolmentId);
        consent.AcceptedAt.Should().Be(enrolment.ConsentAcceptedAt);
    }

    [Fact]
    public async Task FR02_OutdatedConsentWording_IsRefused()
    {
        var person = AnUnenrolledPerson();

        var response = await PostEnrolmentAsync(person, consentVersion: "POPIA-2020-v0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorFrom(response)).Message.Should().Contain("outdated consent wording");
    }

    // -----------------------------------------------------------------------------
    // FR-03  Verify ID with Home Affairs
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task FR03_AnInvalidIdChecksum_IsRejectedBeforeHomeAffairsIsCalled()
    {
        var person = AnUnenrolledPerson();

        // The right length and a real birth date, but the check digit does not add up.
        var broken = person.IdNumber[..12] + (person.IdNumber[12] == '0' ? '1' : '0');

        var response = await PostEnrolmentAsync(person, idNumber: broken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorFrom(response)).Code.Should().Be(ErrorCodes.InvalidIdNumber);

        host.HomeAffairsCalls.Calls.Should().Be(0);
    }

    [Fact]
    public async Task FR03_AnIdNumberOfTheWrongLength_IsRejectedBeforeHomeAffairsIsCalled()
    {
        var response = await PostEnrolmentAsync(AnUnenrolledPerson(), idNumber: "12345");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorFrom(response)).Code.Should().Be(ErrorCodes.InvalidIdNumber);
        host.HomeAffairsCalls.Calls.Should().Be(0);
    }

    [Fact]
    public async Task FR03_NoMatch_BlocksTheEnrolment()
    {
        var person = RosterPerson(HomeAffairsOutcome.NoMatch);

        var response = await PostEnrolmentAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorFrom(response)).Code.Should().Be(ErrorCodes.HomeAffairsNoMatch);

        host.HomeAffairsCalls.Calls.Should().Be(1);
        await AssertNoCustomerAsync(person.IdNumber);
    }

    [Fact]
    public async Task FR03_Deceased_BlocksTheEnrolment()
    {
        var person = RosterPerson(HomeAffairsOutcome.Deceased);

        var response = await PostEnrolmentAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorFrom(response)).Code.Should().Be(ErrorCodes.HomeAffairsDeceased);

        await AssertNoCustomerAsync(person.IdNumber);
    }

    [Fact]
    public async Task FR03_ServiceDown_TellsTheAgentToTryAgainLater()
    {
        var person = RosterPerson(HomeAffairsOutcome.ServiceUnavailable);

        var response = await PostEnrolmentAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var error = await ErrorFrom(response);
        error.Code.Should().Be(ErrorCodes.HomeAffairsUnavailable);
        error.Message.Should().Contain("try again later");
    }

    [Fact]
    public async Task FR03_ARefusedCheck_IsStillRecordedWithItsReference()
    {
        var person = RosterPerson(HomeAffairsOutcome.Deceased);

        await PostEnrolmentAsync(person);

        await using var core = host.OpenCore();
        var enrolment = await core.Enrolments
            .OrderByDescending(e => e.CreatedAt)
            .FirstAsync(e => e.FullName == person.FullName);

        enrolment.Status.Should().Be(EnrolmentStatus.Failed);
        enrolment.HomeAffairsResult.Should().Be(HomeAffairsOutcome.Deceased);
        enrolment.HomeAffairsReference.Should().StartWith("HA-SIM-");

        // A refused enrolment cannot then be pushed through the later steps.
        var error = await CompleteExpectingErrorAsync(enrolment.EnrolmentId, "4821");
        error.Code.Should().Be(ErrorCodes.EnrolmentIncomplete);
    }

    [Fact]
    public async Task FR03_ARefusedCheckWritesAnAuditRowThatNamesNoOne()
    {
        var person = RosterPerson(HomeAffairsOutcome.NoMatch);

        await PostEnrolmentAsync(person);

        await using var core = host.OpenCore();
        var audit = await core.AuditLog
            .OrderByDescending(a => a.Sequence)
            .FirstAsync(a => a.Action == "EnrolmentStarted");

        audit.Details.Should().Contain("NoMatch");

        // Rule 10.5: the row records what happened, not who it happened to.
        audit.Details.Should().NotContain(person.IdNumber);
        SensitiveDataPatterns.FindViolation(audit.Details).Should().BeNull();
    }

    // -----------------------------------------------------------------------------
    // Things the rest of the flow relies on
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task TheIdNumberIsNeverStoredInTheClear()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await using var core = host.OpenCore();
        var enrolment = await core.Enrolments.SingleAsync(e => e.EnrolmentId == enrolmentId);

        enrolment.IdNumberHash.Should().NotContain(person.IdNumber);
        enrolment.IdNumberHash.Should().HaveLength(64);

        // Only the four digits the customer types at a till are kept as a number.
        enrolment.IdDigits7to10Bucket.Should().Be(person.Bucket);
    }

    [Fact]
    public async Task EnrollingTheSamePersonTwice_IsRefused()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.LeftIndex);
        await CompleteAsync(enrolmentId, "4821");

        var response = await PostEnrolmentAsync(person);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorFrom(response)).Message.Should().Contain("already enrolled");
    }

    [Fact]
    public async Task APinThatIsNotFourToSixDigits_IsRefused()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.LeftIndex);

        var error = await CompleteExpectingErrorAsync(enrolmentId, "12");

        error.Code.Should().Be(ErrorCodes.ValidationError);
        error.Message.Should().Contain("4 to 6 digits");
    }

    [Fact]
    public async Task AFinishedEnrolmentCannotBeChangedAgain()
    {
        var person = AnUnenrolledPerson();
        var enrolmentId = await StartEnrolmentAsync(person);

        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.RightIndex);
        await CaptureFingerAsync(enrolmentId, person.FullName, FingerPosition.LeftIndex);
        await CompleteAsync(enrolmentId, "4821");

        var error = await CompleteExpectingErrorAsync(enrolmentId, "4821");

        error.Code.Should().Be(ErrorCodes.EnrolmentIncomplete);
        error.Message.Should().Contain("completed");
    }

    // -----------------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------------

    /// <param name="IdNumber">A valid 13 digit number that no other test has used.</param>
    private sealed record TestPerson(string FullName, string IdNumber, string CellphoneNumber)
    {
        public int Bucket => SouthAfricanIdNumber.Bucket(IdNumber);
    }

    // A fresh SSSS group per person, so every test gets its own ID number, its own
    // matching bucket and no chance of colliding with another test's customer.
    private static int _nextSequence = 1000;

    /// <summary>
    /// Someone Home Affairs has never heard of, with the switchboard told to answer
    /// Match. That is exactly what the switchboard is for: the roster covers the fixed
    /// outcomes, and this covers the endless supply of ordinary people.
    /// </summary>
    private TestPerson AnUnenrolledPerson()
    {
        host.Control.HomeAffairsOverride = HomeAffairsOutcome.Match;

        var sequence = Interlocked.Increment(ref _nextSequence);

        // YYMMDD SSSS C A, with the check digit calculated so the number is genuinely valid.
        var idNumber = SouthAfricanIdNumber.WithCheckDigit($"900101{sequence:D4}08");

        return new TestPerson($"Test Person {sequence}", idNumber, "0821234567");
    }

    private static TestPerson RosterPerson(HomeAffairsOutcome outcome)
    {
        var entry = HomeAffairsRoster.All.First(e => e.Outcome == outcome);

        return new TestPerson(entry.FullName, entry.IdNumber, "0821234567");
    }

    private async Task<Guid> StartEnrolmentAsync(TestPerson person)
    {
        var response = await PostEnrolmentAsync(person);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreateEnrolmentResponse>(Json);
        created!.HomeAffairsResult.Should().Be(HomeAffairsOutcome.Match);

        return created.EnrolmentId;
    }

    private async Task<HttpResponseMessage> PostEnrolmentAsync(
        TestPerson person,
        bool consentAccepted = true,
        string? consentVersion = null,
        string? idNumber = null) =>
        await host.Client.PostAsJsonAsync("/enrolments", new CreateEnrolmentRequest(
            IdNumber: idNumber ?? person.IdNumber,
            FullName: person.FullName,
            CellphoneNumber: person.CellphoneNumber,
            AgentId: SeedCatalogue.DemoAgentId,
            StoreId: SeedCatalogue.Merchants[0].Stores[0].StoreId,
            ConsentTextVersion: consentVersion ?? ConsentVersion,
            ConsentAccepted: consentAccepted,
            FingerprintSampleBase64: await host.ReadFingerprintAsync()), Json);

    private async Task<SubmitVeinSamplesResponse> CaptureFingerAsync(
        Guid enrolmentId,
        string label,
        FingerPosition position)
    {
        var samples = await host.ReadFingerAsync(label, position, SamplesPerFinger);

        var response = await host.Client.PostAsJsonAsync(
            $"/enrolments/{enrolmentId}/vein-samples",
            new SubmitVeinSamplesRequest(
                position,
                [.. samples.Select(s => s.Payload)],
                [.. samples.Select(s => s.QualityScore)]),
            Json);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SubmitVeinSamplesResponse>(Json))!;
    }

    private async Task<CompleteEnrolmentResponse> CompleteAsync(Guid enrolmentId, string pin)
    {
        var response = await PostCompleteAsync(enrolmentId, pin);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CompleteEnrolmentResponse>(Json))!;
    }

    private async Task<ApiError> CompleteExpectingErrorAsync(Guid enrolmentId, string pin)
    {
        var response = await PostCompleteAsync(enrolmentId, pin);
        response.IsSuccessStatusCode.Should().BeFalse();

        return await ErrorFrom(response);
    }

    private async Task<HttpResponseMessage> PostCompleteAsync(Guid enrolmentId, string pin) =>
        await host.Client.PostAsJsonAsync(
            $"/enrolments/{enrolmentId}/complete",
            new CompleteEnrolmentRequest(await host.EncryptPinAsync(pin)),
            Json);

    private async Task AssertNoCustomerAsync(string idNumber)
    {
        await using var core = host.OpenCore();

        // Nothing to compare the hash against here, so check the plainest thing there is:
        // no profile was created for that name at all.
        var name = HomeAffairsRoster.Find(idNumber)!.FullName;
        (await core.Customers.AnyAsync(c => c.FullName == name)).Should().BeFalse();
    }

    private static async Task<ApiError> ErrorFrom(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiError>(Json))!;
}
