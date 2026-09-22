using System.Text.Json;
using FirmlyPaid.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace FirmlyPaid.Shared.Hosting;

/// <summary>
/// Turns anything thrown by a handler into the one agreed error shape.
/// Expected business outcomes keep their code and plain English message; anything else
/// becomes INTERNAL_ERROR so no internal detail reaches a terminal (rule 10.5).
/// </summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (FirmlyPaidException ex)
        {
            // A business outcome, not a fault: log the code only, never the message body.
            logger.LogInformation("Request rejected with {ErrorCode}", ex.Code);
            await WriteAsync(context, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(
                context,
                ErrorCodes.InternalError,
                "Something went wrong on our side. Please try again.");
        }
    }

    private static async Task WriteAsync(HttpContext context, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = ErrorStatusCodes.For(code);
        context.Response.ContentType = "application/json";

        var error = new ApiError(code, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}
