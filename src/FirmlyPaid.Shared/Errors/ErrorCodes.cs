namespace FirmlyPaid.Shared.Errors;

/// <summary>
/// Every error code FirmlyPaid may return. Codes are part of the public contract with
/// tills and terminals, so they are stable strings rather than an enum.
/// </summary>
public static class ErrorCodes
{
    // Matching and liveness
    public const string NoMatch = "NO_MATCH";
    public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
    public const string LivenessFailed = "LIVENESS_FAILED";
    public const string QualityTooLow = "QUALITY_TOO_LOW";

    // PIN
    public const string PinRequired = "PIN_REQUIRED";
    public const string PinWrong = "PIN_WRONG";

    // Accounts
    public const string AccountNotConfirmed = "ACCOUNT_NOT_CONFIRMED";
    public const string TooManyAccounts = "TOO_MANY_ACCOUNTS";

    // Bank
    public const string BankDeclined = "BANK_DECLINED";
    public const string BankTimeout = "BANK_TIMEOUT";

    // Limits and customer state
    public const string LimitExceeded = "LIMIT_EXCEEDED";
    public const string CustomerFrozen = "CUSTOMER_FROZEN";
    public const string RiskBlocked = "RISK_BLOCKED";

    // Terminal trust
    public const string TerminalNotTrusted = "TERMINAL_NOT_TRUSTED";

    // Enrolment
    public const string InvalidIdNumber = "INVALID_ID_NUMBER";
    public const string ConsentRequired = "CONSENT_REQUIRED";
    public const string HomeAffairsNoMatch = "HOME_AFFAIRS_NO_MATCH";
    public const string HomeAffairsDeceased = "HOME_AFFAIRS_DECEASED";
    public const string HomeAffairsUnavailable = "HOME_AFFAIRS_UNAVAILABLE";
    public const string EnrolmentIncomplete = "ENROLMENT_INCOMPLETE";

    // Generic
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string InternalError = "INTERNAL_ERROR";
}
