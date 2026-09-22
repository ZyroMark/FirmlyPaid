namespace FirmlyPaid.Shared.Errors;

/// <summary>
/// Maps each error code to the HTTP status a client should see.
/// Kept in one place so terminal, till and portal clients all behave the same.
/// </summary>
public static class ErrorStatusCodes
{
    private static readonly Dictionary<string, int> Map = new(StringComparer.Ordinal)
    {
        [ErrorCodes.NoMatch] = StatusCodes.Status404NotFound,
        [ErrorCodes.TooManyAttempts] = StatusCodes.Status429TooManyRequests,
        [ErrorCodes.LivenessFailed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.QualityTooLow] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PinRequired] = StatusCodes.Status428PreconditionRequired,
        [ErrorCodes.PinWrong] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AccountNotConfirmed] = StatusCodes.Status409Conflict,
        [ErrorCodes.TooManyAccounts] = StatusCodes.Status409Conflict,
        [ErrorCodes.BankDeclined] = StatusCodes.Status402PaymentRequired,
        [ErrorCodes.BankTimeout] = StatusCodes.Status504GatewayTimeout,
        [ErrorCodes.LimitExceeded] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CustomerFrozen] = StatusCodes.Status403Forbidden,
        [ErrorCodes.RiskBlocked] = StatusCodes.Status403Forbidden,
        [ErrorCodes.TerminalNotTrusted] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.InvalidIdNumber] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ConsentRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.HomeAffairsNoMatch] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.HomeAffairsDeceased] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.HomeAffairsUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.EnrolmentIncomplete] = StatusCodes.Status409Conflict,
        [ErrorCodes.ValidationError] = StatusCodes.Status400BadRequest,
        [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.Conflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorCodes.InternalError] = StatusCodes.Status500InternalServerError,
    };

    public static int For(string code) =>
        Map.TryGetValue(code, out var status) ? status : StatusCodes.Status400BadRequest;
}
