using System.Text;
using FirmlyPaid.Data.Core;
using FirmlyPaid.Data.Core.Entities;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.Enrolment.Api;

/// <summary>
/// Agent-assisted sign-up, in three steps: verify the person, capture two fingers, set a
/// PIN. Nothing biometric is stored before consent is recorded and Home Affairs has
/// answered, and the ID number itself is never written anywhere (rules 10.4 and 10.5).
/// </summary>
public sealed class EnrolmentService(
    FirmlyPaidCoreDbContext database,
    IIdentityHasher identityHasher,
    IPinHasher pinHasher,
    IKeyVault keyVault,
    IHomeAffairsVerifier homeAffairs,
    IMatchingClient matching,
    IOptions<FirmlyPaidOptions> options,
    IClock clock,
    ILogger<EnrolmentService> logger)
{
    private readonly EnrolmentOptions _options = options.Value.Enrolment;

    /// <summary>
    /// Step one: check the ID number, record consent, and ask Home Affairs whether this
    /// person is who they say they are (FR-02, FR-03).
    /// </summary>
    public async Task<CreateEnrolmentResponse> CreateAsync(CreateEnrolmentRequest request, CancellationToken ct = default)
    {
        ValidatePersonalDetails(request);

        // FR-03: a number with a bad checksum is rejected here, before Home Affairs is
        // called at all. The partner charges per call and would only say no anyway.
        if (!SouthAfricanIdNumber.IsValid(request.IdNumber))
        {
            throw new FirmlyPaidException(
                ErrorCodes.InvalidIdNumber,
                "That ID number is not valid. Please check the 13 digits and try again.");
        }

        // FR-02: consent comes before anything else is captured, not after.
        if (!request.ConsentAccepted)
        {
            throw new FirmlyPaidException(
                ErrorCodes.ConsentRequired,
                "The customer must accept the FirmlyPaid consent before enrolment can start.");
        }

        if (request.ConsentTextVersion != _options.ConsentTextVersion)
        {
            throw FirmlyPaidException.Validation(
                $"This enrolment app is showing outdated consent wording. Expected {_options.ConsentTextVersion}.");
        }

        var fingerprintSample = DecodeFingerprintSample(request.FingerprintSampleBase64);

        var idNumberHash = identityHasher.HashIdNumber(request.IdNumber);

        if (await database.Customers.AnyAsync(c => c.IdNumberHash == idNumberHash, ct))
        {
            throw new FirmlyPaidException(
                ErrorCodes.Conflict,
                "This person is already enrolled with FirmlyPaid.");
        }

        var verification = await homeAffairs.VerifyAsync(
            new HomeAffairsVerificationRequest(request.IdNumber, request.FullName, fingerprintSample),
            ct);

        var now = clock.UtcNow;

        var enrolment = new Data.Core.Entities.Enrolment
        {
            AgentId = request.AgentId,
            StoreId = request.StoreId,
            FullName = request.FullName.Trim(),
            IdNumberHash = idNumberHash,
            IdDigits7to10Bucket = SouthAfricanIdNumber.Bucket(request.IdNumber),
            CellphoneNumber = request.CellphoneNumber.Trim(),
            ConsentTextVersion = request.ConsentTextVersion,
            ConsentAcceptedAt = now,
            HomeAffairsResult = verification.Outcome,
            HomeAffairsReference = verification.Reference,
            // A failed check is still recorded: the agent needs the reference, and a
            // refused enrolment is exactly the kind of thing an auditor asks about.
            Status = verification.IsMatch ? EnrolmentStatus.InProgress : EnrolmentStatus.Failed,
            CreatedAt = now,
        };

        database.Enrolments.Add(enrolment);

        await database.AppendAuditAsync(
            actor: $"agent:{request.AgentId}",
            action: "EnrolmentStarted",
            entityType: "Enrolment",
            entityId: enrolment.EnrolmentId.ToString(),
            // Identifiers live in EntityType and EntityId, never in the prose: an audit
            // detail that carried a GUID could trip the sensitive data check on a run of
            // digits and refuse to record a legitimate enrolment (rule 10.5).
            details: $"Home Affairs answered {verification.Outcome} against reference {verification.Reference}.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);

        // Thrown after the row is committed, so the refusal leaves a trail.
        ThrowIfHomeAffairsRefused(verification.Outcome);

        logger.LogInformation("Enrolment {EnrolmentId} started at store {StoreId}", enrolment.EnrolmentId, request.StoreId);

        return new CreateEnrolmentResponse(enrolment.EnrolmentId, verification.Outcome, verification.Reference);
    }

    /// <summary>
    /// Step two: three samples of one finger. Poor reads are turned away with a retry
    /// message rather than stored, and two different fingers are needed in all (FR-01).
    /// </summary>
    public async Task<SubmitVeinSamplesResponse> SubmitVeinSamplesAsync(
        Guid enrolmentId,
        SubmitVeinSamplesRequest request,
        CancellationToken ct = default)
    {
        var enrolment = await RequireInProgressAsync(enrolmentId, ct);

        if (request.FingerPosition == FingerPosition.Unknown)
        {
            throw FirmlyPaidException.Validation("Please choose which finger is being captured.");
        }

        if (request.Samples.Count != _options.SamplesPerFinger)
        {
            throw FirmlyPaidException.Validation(
                $"Exactly {_options.SamplesPerFinger} samples are needed for each finger.");
        }

        if (request.QualityScores.Count != request.Samples.Count)
        {
            throw FirmlyPaidException.Validation("Every sample needs a quality score.");
        }

        // Checked before the vault is called so a poor read never leaves this service.
        var worstQuality = request.QualityScores.Min();
        if (worstQuality < _options.MinimumQualityScore)
        {
            logger.LogInformation(
                "Enrolment {EnrolmentId} rejected a {Position} capture scoring {Quality}",
                enrolmentId,
                request.FingerPosition,
                worstQuality);

            return new SubmitVeinSamplesResponse(
                Accepted: false,
                AverageQualityScore: (int)Math.Round(request.QualityScores.Average()),
                FingersCaptured: enrolment.FingersCaptured,
                RetryMessage: "That read was not clear enough. Please wipe the sensor and try the same finger again.");
        }

        var stored = await matching.StoreTemplatesAsync(
            new StoreTemplatesRequest(
                enrolment.TemplateOwnerId,
                enrolment.IdDigits7to10Bucket,
                request.FingerPosition,
                request.Samples,
                request.QualityScores),
            ct);

        if (!stored.Accepted)
        {
            return new SubmitVeinSamplesResponse(
                Accepted: false,
                AverageQualityScore: stored.AverageQualityScore,
                FingersCaptured: enrolment.FingersCaptured,
                RetryMessage: stored.RejectionReason ?? "Please capture that finger again.");
        }

        // The vault is the one place that knows how many fingers are really on file.
        enrolment.FingersCaptured = stored.FingersHeld;

        await database.AppendAuditAsync(
            actor: $"agent:{enrolment.AgentId}",
            action: "VeinSamplesCaptured",
            entityType: "Enrolment",
            entityId: enrolment.EnrolmentId.ToString(),
            details: $"Captured {request.Samples.Count} samples of the {request.FingerPosition} finger " +
                     $"at an average quality of {stored.AverageQualityScore}. " +
                     $"{stored.FingersHeld} finger(s) now on file.",
            createdAtUtc: clock.UtcNow,
            ct: ct);

        await database.SaveChangesAsync(ct);

        return new SubmitVeinSamplesResponse(
            Accepted: true,
            AverageQualityScore: stored.AverageQualityScore,
            FingersCaptured: stored.FingersHeld,
            RetryMessage: null);
    }

    /// <summary>
    /// Step three: the customer chooses a PIN and the profile goes live. Refused unless
    /// Home Affairs matched and both fingers are on file (FR-01).
    /// </summary>
    public async Task<CompleteEnrolmentResponse> CompleteAsync(
        Guid enrolmentId,
        CompleteEnrolmentRequest request,
        CancellationToken ct = default)
    {
        var enrolment = await RequireInProgressAsync(enrolmentId, ct);

        if (enrolment.HomeAffairsResult != HomeAffairsOutcome.Match)
        {
            throw new FirmlyPaidException(
                ErrorCodes.EnrolmentIncomplete,
                "This enrolment cannot be completed because the Home Affairs check did not pass.");
        }

        if (enrolment.FingersCaptured < _options.RequiredFingers)
        {
            throw new FirmlyPaidException(
                ErrorCodes.EnrolmentIncomplete,
                $"{_options.RequiredFingers} different fingers must be captured before the PIN step.");
        }

        var pin = await DecryptPinAsync(request.EncryptedPin, ct);
        PinRules.EnsureValid(pin);

        var now = clock.UtcNow;

        var customer = new Customer
        {
            FullName = enrolment.FullName,
            IdNumberHash = enrolment.IdNumberHash,
            IdDigits7to10Bucket = enrolment.IdDigits7to10Bucket,
            CellphoneNumber = enrolment.CellphoneNumber,
            PinHash = pinHasher.Hash(pin),
            Status = CustomerStatus.Active,
            Tier = CustomerTier.Pilot,
            // The same handle the vault already holds templates under. This is the only
            // line that ties a person to their biometrics.
            TemplateOwnerId = enrolment.TemplateOwnerId,
            CreatedAt = now,
        };

        database.Customers.Add(customer);

        // FR-02: the consent row carries the wording, the agent and the time the customer
        // actually accepted, not the time the profile happened to be created.
        database.Consents.Add(new Consent
        {
            CustomerId = customer.CustomerId,
            ConsentTextVersion = enrolment.ConsentTextVersion,
            AgentId = enrolment.AgentId,
            AcceptedAt = enrolment.ConsentAcceptedAt,
        });

        enrolment.CustomerId = customer.CustomerId;
        enrolment.Status = EnrolmentStatus.Completed;
        enrolment.CompletedAt = now;

        await database.AppendAuditAsync(
            actor: $"agent:{enrolment.AgentId}",
            action: "EnrolmentCompleted",
            entityType: "Enrolment",
            entityId: enrolment.EnrolmentId.ToString(),
            details: $"Completed with {enrolment.FingersCaptured} fingers on file. The customer profile is now active.",
            createdAtUtc: now,
            ct: ct);

        await database.SaveChangesAsync(ct);

        logger.LogInformation("Enrolment {EnrolmentId} completed as customer {CustomerId}", enrolmentId, customer.CustomerId);

        return new CompleteEnrolmentResponse(customer.CustomerId, customer.Status);
    }

    private void ValidatePersonalDetails(CreateEnrolmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw FirmlyPaidException.Validation("The customer's full name is required.");
        }

        if (!IsPlausibleCellphone(request.CellphoneNumber))
        {
            throw FirmlyPaidException.Validation("A valid cellphone number is required.");
        }

        if (request.AgentId == Guid.Empty)
        {
            throw FirmlyPaidException.Validation("The agent's id is required.");
        }

        if (request.StoreId == Guid.Empty)
        {
            throw FirmlyPaidException.Validation("The store id is required.");
        }
    }

    /// <summary>A South African cellphone: 10 digits local, or the same with +27.</summary>
    private static bool IsPlausibleCellphone(string? cellphone)
    {
        if (string.IsNullOrWhiteSpace(cellphone))
        {
            return false;
        }

        var digits = cellphone.Trim().TrimStart('+');

        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return digits.Length is >= 10 and <= 12;
    }

    private static byte[] DecodeFingerprintSample(string? sampleBase64)
    {
        if (string.IsNullOrWhiteSpace(sampleBase64))
        {
            throw FirmlyPaidException.Validation(
                "A fingerprint sample is required for the Home Affairs check.");
        }

        try
        {
            var sample = Convert.FromBase64String(sampleBase64);

            return sample.Length == 0
                ? throw FirmlyPaidException.Validation("The fingerprint sample is empty.")
                : sample;
        }
        catch (FormatException)
        {
            throw FirmlyPaidException.Validation("The fingerprint sample is not valid base64.");
        }
    }

    private async Task<string> DecryptPinAsync(EncryptedPayloadDto encryptedPin, CancellationToken ct)
    {
        if (encryptedPin is null)
        {
            throw FirmlyPaidException.Validation("The PIN is required.");
        }

        // Rule 10.6: the PIN travels encrypted from the terminal. It exists in the clear
        // here only long enough to be hashed, and is never logged or stored.
        var plaintext = await keyVault.UnprotectAsync(encryptedPin.ToPayload(), ct);

        return Encoding.UTF8.GetString(plaintext);
    }

    private async Task<Data.Core.Entities.Enrolment> RequireInProgressAsync(Guid enrolmentId, CancellationToken ct)
    {
        var enrolment = await database.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId, ct)
            ?? throw FirmlyPaidException.NotFound("That enrolment");

        return enrolment.Status == EnrolmentStatus.InProgress
            ? enrolment
            : throw new FirmlyPaidException(
                ErrorCodes.EnrolmentIncomplete,
                $"That enrolment is {enrolment.Status.ToString().ToLowerInvariant()} and cannot be changed.");
    }

    /// <summary>Turns a Home Affairs refusal into the code the agent's screen knows (FR-03).</summary>
    private static void ThrowIfHomeAffairsRefused(HomeAffairsOutcome outcome)
    {
        switch (outcome)
        {
            case HomeAffairsOutcome.Match:
                return;

            case HomeAffairsOutcome.NoMatch:
                throw new FirmlyPaidException(
                    ErrorCodes.HomeAffairsNoMatch,
                    "Home Affairs could not match this person's fingerprint to that ID number.");

            case HomeAffairsOutcome.Deceased:
                throw new FirmlyPaidException(
                    ErrorCodes.HomeAffairsDeceased,
                    "That ID number is marked deceased on the population register.");

            default:
                throw new FirmlyPaidException(
                    ErrorCodes.HomeAffairsUnavailable,
                    "Home Affairs is not answering at the moment. Please try again later.");
        }
    }
}
