namespace FirmlyPaid.Shared.Errors;

/// <summary>
/// The single error shape every FirmlyPaid service returns: { code, message, traceId }.
/// </summary>
/// <param name="Code">A stable machine-readable code from <see cref="ErrorCodes"/>.</param>
/// <param name="Message">Plain English, safe to show a cashier or customer.</param>
/// <param name="TraceId">Correlates the response with server logs.</param>
public sealed record ApiError(string Code, string Message, string TraceId);
