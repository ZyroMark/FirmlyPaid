using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Contracts;

/// <summary>POST /enrolments</summary>
public sealed record CreateEnrolmentRequest(
    string IdNumber,
    string FullName,
    string CellphoneNumber,
    Guid AgentId,
    Guid StoreId,
    string ConsentTextVersion,
    bool ConsentAccepted,
    string FingerprintSampleBase64);

public sealed record CreateEnrolmentResponse(
    Guid EnrolmentId,
    HomeAffairsOutcome HomeAffairsResult,
    string HomeAffairsReference);

/// <summary>POST /enrolments/{id}/vein-samples. Three samples for one finger position.</summary>
public sealed record SubmitVeinSamplesRequest(
    FingerPosition FingerPosition,
    IReadOnlyList<EncryptedPayloadDto> Samples,
    IReadOnlyList<int> QualityScores);

public sealed record SubmitVeinSamplesResponse(
    bool Accepted,
    int AverageQualityScore,
    int FingersCaptured,
    string? RetryMessage);

/// <summary>POST /enrolments/{id}/complete</summary>
public sealed record CompleteEnrolmentRequest(EncryptedPayloadDto EncryptedPin);

public sealed record CompleteEnrolmentResponse(Guid CustomerId, CustomerStatus Status);
