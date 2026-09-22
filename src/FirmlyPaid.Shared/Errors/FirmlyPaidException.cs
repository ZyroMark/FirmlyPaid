using FirmlyPaid.Shared.Money;

namespace FirmlyPaid.Shared.Errors;

/// <summary>
/// Thrown for an expected business outcome that the caller must see as a coded error.
/// Anything else that escapes becomes INTERNAL_ERROR so internals never leak.
/// </summary>
public class FirmlyPaidException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public static FirmlyPaidException NoMatch() =>
        new(ErrorCodes.NoMatch, "We could not recognise that finger. Please try again.");

    public static FirmlyPaidException TooManyAttempts() =>
        new(ErrorCodes.TooManyAttempts, "Too many tries. Please use another payment method.");

    public static FirmlyPaidException LivenessFailed() =>
        new(ErrorCodes.LivenessFailed, "The scanner could not confirm a live finger. Please try again.");

    public static FirmlyPaidException PinRequired() =>
        new(ErrorCodes.PinRequired, "Please enter your PIN to approve this payment.");

    public static FirmlyPaidException PinWrong() =>
        new(ErrorCodes.PinWrong, "That PIN is not correct.");

    public static FirmlyPaidException AccountNotConfirmed() =>
        new(ErrorCodes.AccountNotConfirmed, "That bank account is not confirmed yet.");

    public static FirmlyPaidException LimitExceeded(decimal limit) =>
        new(ErrorCodes.LimitExceeded, $"This payment is above your limit of {Rand.Format2(limit)}.");

    public static FirmlyPaidException CustomerFrozen() =>
        new(ErrorCodes.CustomerFrozen, "This profile is frozen. Please contact FirmlyPaid support.");

    public static FirmlyPaidException TerminalNotTrusted() =>
        new(ErrorCodes.TerminalNotTrusted, "This terminal is not trusted.");

    public static FirmlyPaidException NotFound(string what) =>
        new(ErrorCodes.NotFound, $"{what} was not found.");

    public static FirmlyPaidException Validation(string message) =>
        new(ErrorCodes.ValidationError, message);
}
